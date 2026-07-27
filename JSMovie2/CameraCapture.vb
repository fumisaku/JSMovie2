Imports System.Runtime.InteropServices
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading
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

    '====== コンストラクタ ======
    Public Sub New(cameraIndex As Integer)
        _cameraIndex = cameraIndex
    End Sub

    ''' <summary>カメラを開始しWPF Imageコントロールに表示する</summary>
    Public Function Start(frameBuffer As System.Windows.Controls.Image) As Boolean
        _frameImage = frameBuffer
        Try
            ' バックエンドを順に試行: MSMF → DSHOW → ANY
            Dim backends() As VideoCaptureAPIs = {
                VideoCaptureAPIs.MSMF,
                VideoCaptureAPIs.DSHOW,
                VideoCaptureAPIs.ANY
            }

            For Each backend In backends
                Try
                    _vcap = New VideoCapture(_cameraIndex, backend)
                    System.Threading.Thread.Sleep(500)
                    If _vcap.IsOpened() Then
                        ' 実際にフレームが読めるか確認
                        Using testFrame As New Mat()
                            If _vcap.Read(testFrame) AndAlso Not testFrame.Empty() Then
                                Exit For  ' 成功
                            End If
                        End Using
                    End If
                    _vcap.Dispose()
                    _vcap = Nothing
                Catch
                    If _vcap IsNot Nothing Then _vcap.Dispose() : _vcap = Nothing
                End Try
            Next

            If _vcap Is Nothing OrElse Not _vcap.IsOpened() Then
                RaiseEvent CameraError(Me, "カメラ番号 " & _cameraIndex & " をオープンできませんでした。")
                Return False
            End If

            ' 解像度・FPS 設定
            _vcap.Set(VideoCaptureProperties.FrameWidth, _frameWidth)
            _vcap.Set(VideoCaptureProperties.FrameHeight, _frameHeight)
            _vcap.Set(VideoCaptureProperties.Fps, _fps)

            ' 実際に取得できた解像度を反映
            _frameWidth = CInt(_vcap.Get(VideoCaptureProperties.FrameWidth))
            _frameHeight = CInt(_vcap.Get(VideoCaptureProperties.FrameHeight))
            Dim actualFps As Double = _vcap.Get(VideoCaptureProperties.Fps)
            If actualFps > 0 Then _fps = CInt(actualFps)

            ' WriteableBitmap 初期化
            _writeableBitmap = New WriteableBitmap(
                _frameWidth, _frameHeight, 96, 96, PixelFormats.Bgr24, Nothing)
            _frameImage.Source = _writeableBitmap

            ' 取得タイマー開始
            _captureTimer = New DispatcherTimer()
            _captureTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / _fps)
            AddHandler _captureTimer.Tick, AddressOf OnCaptureTick
            _captureTimer.Start()
            _isRunning = True
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

                ' BGR24 バイト配列に変換
                Dim stride As Integer = mat.Width * 3
                Dim frameData(mat.Width * mat.Height * 3 - 1) As Byte
                Marshal.Copy(mat.Data, frameData, 0, frameData.Length)

                ' WriteableBitmap に書き込む (GDI 不使用)
                _writeableBitmap.Lock()
                Try
                    _writeableBitmap.WritePixels(
                        New Int32Rect(0, 0, mat.Width, mat.Height),
                        frameData, stride, 0)
                    _writeableBitmap.AddDirtyRect(New Int32Rect(0, 0, mat.Width, mat.Height))
                Finally
                    _writeableBitmap.Unlock()
                End Try

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
