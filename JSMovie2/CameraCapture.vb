Imports System.Runtime.InteropServices
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading
Imports System.Threading.Tasks
Imports OpenCvSharp
Imports OpenCvSharp.Extensions

''' <summary>
''' カメラキャプチャエンジン
''' OpenCvSharp4 (4.10.x) を使用。旧版(4.5.5)と異なり Windows 11 で動作する。
''' 映像表示: WriteableBitmap (WPF 直接描画、GDI 不使用)
''' 映像録画: ffmpeg.exe にフレームをパイプ入力 (VideoWriter 不使用)
''' </summary>
Public Class CameraCapture
    Implements IDisposable

    '====== パラメータ ======
    Private _cameraIndex As Integer
    Private _frameWidth As Integer = 1440
    Private _frameHeight As Integer = 810
    Private _fps As Integer = 30
    Private _isRunning As Boolean = False
    Private _isRecording As Boolean = False

    Private _captureTimer As DispatcherTimer
    Private _writeableBitmap As WriteableBitmap
    Private _frameImage As System.Windows.Controls.Image  ' 表示先

    ' OpenCvSharp
    Private _vcap As VideoCapture

    ' ffmpeg パイプ録画
    Private _ffmpegProcess As System.Diagnostics.Process
    Private _ffmpegStream As System.IO.Stream

    '====== イベント ======
    Public Event CameraError(sender As Object, message As String)
    Public Event CameraStarted(sender As Object, e As EventArgs)

    '====== コンストラクタ ======
    Public Sub New(cameraIndex As Integer)
        _cameraIndex = cameraIndex
    End Sub

    ''' <summary>
    ''' カメラを非同期で起動しWPF Imageコントロールに表示する。
    ''' カメラ初期化はバックグラウンドスレッドで行い、UIスレッドをブロックしない。
    ''' </summary>
    Public Async Function StartAsync(frameBuffer As System.Windows.Controls.Image) As Task(Of Boolean)
        _frameImage = frameBuffer
        Try
            ' ---- バックグラウンドでカメラ初期化 ----
            Dim vcap As VideoCapture = Nothing
            Dim actualW As Integer = _frameWidth
            Dim actualH As Integer = _frameHeight
            Dim actualFps As Integer = _fps

            Dim success As Boolean = Await Task.Run(Function()
                ' バックエンドを順に試行: MSMF → DSHOW → ANY
                Dim backends() As VideoCaptureAPIs = {
                    VideoCaptureAPIs.MSMF,
                    VideoCaptureAPIs.DSHOW,
                    VideoCaptureAPIs.ANY
                }
                For Each backend In backends
                    Try
                        Dim v As New VideoCapture(_cameraIndex, backend)
                        ' カメラデバイスの初期化待ち（バックグラウンドなのでSleepしてOK）
                        System.Threading.Thread.Sleep(500)
                        If v.IsOpened() Then
                            Using testFrame As New Mat()
                                If v.Read(testFrame) AndAlso Not testFrame.Empty() Then
                                    vcap = v
                                    Return True  ' 成功
                                End If
                            End Using
                        End If
                        v.Dispose()
                    Catch
                    End Try
                Next
                Return False
            End Function)

            If Not success OrElse vcap Is Nothing Then
                RaiseEvent CameraError(Me, "カメラ番号 " & _cameraIndex & " をオープンできませんでした。")
                Return False
            End If

            ' ---- 以降はUIスレッドで実行 ----
            _vcap = vcap

            ' 解像度・FPS 設定
            _vcap.Set(VideoCaptureProperties.FrameWidth, _frameWidth)
            _vcap.Set(VideoCaptureProperties.FrameHeight, _frameHeight)
            _vcap.Set(VideoCaptureProperties.Fps, _fps)

            ' 実際に取得できた解像度を反映
            _frameWidth = CInt(_vcap.Get(VideoCaptureProperties.FrameWidth))
            _frameHeight = CInt(_vcap.Get(VideoCaptureProperties.FrameHeight))
            If _frameWidth <= 0 Then _frameWidth = 1280
            If _frameHeight <= 0 Then _frameHeight = 720

            Dim fps As Double = _vcap.Get(VideoCaptureProperties.Fps)
            If fps > 0 AndAlso fps <= 120 Then _fps = CInt(fps)

            ' WriteableBitmap 初期化（UIスレッドで作成）
            _writeableBitmap = New WriteableBitmap(
                _frameWidth, _frameHeight, 96, 96, PixelFormats.Bgr24, Nothing)
            _frameImage.Source = _writeableBitmap

            ' 取得タイマー開始
            _captureTimer = New DispatcherTimer(DispatcherPriority.Render)
            _captureTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / _fps)
            AddHandler _captureTimer.Tick, AddressOf OnCaptureTick
            _captureTimer.Start()
            _isRunning = True

            RaiseEvent CameraStarted(Me, EventArgs.Empty)
            Return True

        Catch ex As Exception
            RaiseEvent CameraError(Me, "カメラ起動エラー: " & ex.Message)
            Return False
        End Try
    End Function

    ''' <summary>フレーム取得タイマーのコールバック (UIスレッドで実行)</summary>
    Private Sub OnCaptureTick(sender As Object, e As EventArgs)
        If Not _isRunning OrElse _vcap Is Nothing OrElse Not _vcap.IsOpened() Then Exit Sub

        Try
            Using mat As New Mat()
                If Not _vcap.Read(mat) OrElse mat.Empty() Then Exit Sub

                ' BGR24 に変換（カメラによって異なるフォーマットに対応）
                Dim bgrMat As Mat
                If mat.Type() = MatType.CV_8UC3 Then
                    bgrMat = mat  ' すでにBGR24
                ElseIf mat.Type() = MatType.CV_8UC4 Then
                    ' BGRA → BGR
                    bgrMat = New Mat()
                    Cv2.CvtColor(mat, bgrMat, ColorConversionCodes.BGRA2BGR)
                ElseIf mat.Type() = MatType.CV_8UC1 Then
                    ' グレースケール → BGR
                    bgrMat = New Mat()
                    Cv2.CvtColor(mat, bgrMat, ColorConversionCodes.GRAY2BGR)
                Else
                    ' その他: そのまま使用
                    bgrMat = mat
                End If

                Dim w As Integer = bgrMat.Width
                Dim h As Integer = bgrMat.Height
                If w <= 0 OrElse h <= 0 Then Exit Sub

                Dim stride As Integer = w * 3
                Dim frameData(stride * h - 1) As Byte
                Marshal.Copy(bgrMat.Data, frameData, 0, frameData.Length)

                If Not Object.ReferenceEquals(bgrMat, mat) Then bgrMat.Dispose()

                ' WriteableBitmapのサイズが変わっていたら再作成
                If _writeableBitmap.PixelWidth <> w OrElse _writeableBitmap.PixelHeight <> h Then
                    _writeableBitmap = New WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr24, Nothing)
                    _frameImage.Source = _writeableBitmap
                End If

                ' WriteableBitmap に書き込む（WritePixels は Lock/Unlock 不要）
                _writeableBitmap.WritePixels(New Int32Rect(0, 0, w, h), frameData, stride, 0)

                ' 録画中は ffmpeg にフレームを送る
                If _isRecording AndAlso _ffmpegStream IsNot Nothing Then
                    Try
                        _ffmpegStream.Write(frameData, 0, frameData.Length)
                    Catch
                    End Try
                End If
            End Using
        Catch
            ' フレーム取得エラーは無視して続行
        End Try
    End Sub

    ''' <summary>ffmpeg パイプ入力で録画開始</summary>
    Public Function StartRecording(outputVideoPath As String) As Boolean
        If _isRecording Then Return False

        Dim ffmpegExe As String = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe")
        If Not System.IO.File.Exists(ffmpegExe) Then
            RaiseEvent CameraError(Me, "ffmpeg.exe が見つかりません: " & ffmpegExe)
            Return False
        End If

        Try
            Dim args As String = String.Format(
                "-y -f rawvideo -pix_fmt bgr24 -s {0}x{1} -r {2} -i pipe:0 " &
                "-c:v libx264 -preset fast -crf 23 ""{3}""",
                _frameWidth, _frameHeight, _fps, outputVideoPath)

            Dim psi As New System.Diagnostics.ProcessStartInfo(ffmpegExe, args)
            psi.UseShellExecute = False
            psi.RedirectStandardInput = True
            psi.CreateNoWindow = True
            psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden

            _ffmpegProcess = System.Diagnostics.Process.Start(psi)
            _ffmpegStream = _ffmpegProcess.StandardInput.BaseStream
            _isRecording = True
            Return True
        Catch ex As Exception
            RaiseEvent CameraError(Me, "録画開始エラー: " & ex.Message)
            Return False
        End Try
    End Function

    ''' <summary>録画停止</summary>
    Public Sub StopRecording()
        If Not _isRecording Then Exit Sub
        _isRecording = False
        Try
            _ffmpegStream?.Flush()
            _ffmpegStream?.Close()
            _ffmpegStream = Nothing
            If _ffmpegProcess IsNot Nothing AndAlso Not _ffmpegProcess.HasExited Then
                _ffmpegProcess.WaitForExit(10000)
            End If
            _ffmpegProcess?.Dispose()
            _ffmpegProcess = Nothing
        Catch
        End Try
    End Sub

    ''' <summary>カメラ停止</summary>
    Public Sub [Stop]()
        _isRunning = False
        _captureTimer?.Stop()
        _captureTimer = Nothing
        StopRecording()
        _vcap?.Release()
        _vcap?.Dispose()
        _vcap = Nothing
    End Sub

    Public Property FrameWidth As Integer
        Get
            Return _frameWidth
        End Get
        Set(value As Integer)
            _frameWidth = value
        End Set
    End Property

    Public Property FrameHeight As Integer
        Get
            Return _frameHeight
        End Get
        Set(value As Integer)
            _frameHeight = value
        End Set
    End Property

    Public Property Fps As Integer
        Get
            Return _fps
        End Get
        Set(value As Integer)
            _fps = value
        End Set
    End Property

    Public ReadOnly Property IsRecording As Boolean
        Get
            Return _isRecording
        End Get
    End Property

    Public Sub Dispose() Implements IDisposable.Dispose
        [Stop]()
    End Sub

End Class

''' <summary>フレーム受信イベント引数</summary>
Public Class FrameReceivedEventArgs
    Inherits EventArgs
    Public ReadOnly Property FrameData() As Byte()
    Public ReadOnly Property Width As Integer
    Public ReadOnly Property Height As Integer
    Public Sub New(data() As Byte, w As Integer, h As Integer)
        FrameData = data
        Width = w
        Height = h
    End Sub
End Class
