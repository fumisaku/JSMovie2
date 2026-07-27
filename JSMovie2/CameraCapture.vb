Imports System.Runtime.InteropServices
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading
Imports System.Threading
Imports System.Threading.Tasks
Imports OpenCvSharp

''' <summary>
''' カメラキャプチャエンジン
''' OpenCvSharp4 (4.10.x) を使用。
''' フレーム取得: 専用バックグラウンドスレッド（DispatcherTimer 不使用）
''' 映像表示:    WriteableBitmap を Dispatcher.Invoke でUIスレッドに書き込み
''' 映像録画:    ffmpeg.exe にフレームをパイプ入力
''' </summary>
Public Class CameraCapture
    Implements IDisposable

    '====== パラメータ ======
    Private _cameraIndex As Integer
    Private _frameWidth As Integer = 1280
    Private _frameHeight As Integer = 720
    Private _fps As Integer = 30
    Private _isRunning As Boolean = False
    Private _isRecording As Boolean = False

    Private _writeableBitmap As WriteableBitmap
    Private _frameImage As System.Windows.Controls.Image  ' 表示先
    Private _dispatcher As Dispatcher

    ' OpenCvSharp
    Private _vcap As VideoCapture

    ' フレーム取得スレッド
    Private _captureThread As Thread

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
        _dispatcher = frameBuffer.Dispatcher

        Try
            ' ---- バックグラウンドでカメラ初期化 ----
            Dim vcap As VideoCapture = Nothing

            Dim success As Boolean = Await Task.Run(Function()
                ' DSHOW を最初に試す（MSMFはWindows11でフレームが黒になる既知問題あり）
                Dim backends() As VideoCaptureAPIs = {
                    VideoCaptureAPIs.DSHOW,
                    VideoCaptureAPIs.MSMF,
                    VideoCaptureAPIs.ANY
                }
                For Each backend In backends
                    Try
                        Dim v As New VideoCapture(_cameraIndex, backend)
                        Thread.Sleep(800)
                        If v.IsOpened() Then
                            Dim ok As Boolean = False
                            For i As Integer = 0 To 4
                                Using testFrame As New Mat()
                                    If v.Read(testFrame) AndAlso Not testFrame.Empty() Then
                                        ok = True
                                        Exit For
                                    End If
                                End Using
                                Thread.Sleep(100)
                            Next
                            If ok Then
                                vcap = v
                                Return True
                            End If
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

            ' ---- UIスレッドで後処理 ----
            _vcap = vcap

            ' 解像度・FPS 設定（カメラが対応していなくても続行）
            _vcap.Set(VideoCaptureProperties.FrameWidth, _frameWidth)
            _vcap.Set(VideoCaptureProperties.FrameHeight, _frameHeight)
            _vcap.Set(VideoCaptureProperties.Fps, _fps)

            ' 実際の解像度を取得（0の場合はデフォルト値を使用）
            Dim w As Integer = CInt(_vcap.Get(VideoCaptureProperties.FrameWidth))
            Dim h As Integer = CInt(_vcap.Get(VideoCaptureProperties.FrameHeight))
            If w > 0 Then _frameWidth = w
            If h > 0 Then _frameHeight = h

            Dim fpsVal As Double = _vcap.Get(VideoCaptureProperties.Fps)
            If fpsVal > 0 AndAlso fpsVal <= 120 Then _fps = CInt(fpsVal)

            ' WriteableBitmap を UIスレッドで生成
            _writeableBitmap = New WriteableBitmap(
                _frameWidth, _frameHeight, 96, 96, PixelFormats.Bgr24, Nothing)
            _frameImage.Source = _writeableBitmap

            ' フレーム取得スレッド開始
            _isRunning = True
            _captureThread = New Thread(AddressOf CaptureLoop)
            _captureThread.IsBackground = True
            _captureThread.Name = "CameraCapture"
            _captureThread.Start()

            RaiseEvent CameraStarted(Me, EventArgs.Empty)
            Return True

        Catch ex As Exception
            RaiseEvent CameraError(Me, "カメラ起動エラー: " & ex.Message)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' バックグラウンドスレッドでフレームを連続取得し、UIスレッドに描画する。
    ''' </summary>
    Private Sub CaptureLoop()
        Dim intervalMs As Integer = CInt(1000.0 / _fps)

        Do While _isRunning
            Dim sw = System.Diagnostics.Stopwatch.StartNew()

            Try
                If _vcap Is Nothing OrElse Not _vcap.IsOpened() Then Exit Do

                Using mat As New Mat()
                    If Not _vcap.Read(mat) OrElse mat.Empty() Then Continue Do

                    Dim bgrMat As Mat = ToBgr24(mat)
                    If bgrMat Is Nothing Then Continue Do

                    Dim fw As Integer = bgrMat.Width
                    Dim fh As Integer = bgrMat.Height
                    Dim stride As Integer = fw * 3
                    Dim frameData(stride * fh - 1) As Byte
                    Marshal.Copy(bgrMat.Data, frameData, 0, frameData.Length)

                    If Not Object.ReferenceEquals(bgrMat, mat) Then bgrMat.Dispose()

                    If _isRecording AndAlso _ffmpegStream IsNot Nothing Then
                        Try
                            _ffmpegStream.Write(frameData, 0, frameData.Length)
                        Catch
                        End Try
                    End If

                    _dispatcher.Invoke(
                        Sub()
                            If Not _isRunning Then Exit Sub
                            Try
                                If _writeableBitmap Is Nothing OrElse
                                   _writeableBitmap.PixelWidth <> fw OrElse
                                   _writeableBitmap.PixelHeight <> fh Then
                                    _writeableBitmap = New WriteableBitmap(
                                        fw, fh, 96, 96, PixelFormats.Bgr24, Nothing)
                                    _frameImage.Source = _writeableBitmap
                                End If
                                _writeableBitmap.WritePixels(
                                    New Int32Rect(0, 0, fw, fh), frameData, stride, 0)
                            Catch
                            End Try
                        End Sub,
                        DispatcherPriority.Render)
                End Using

            Catch
            End Try

            sw.Stop()
            Dim remaining As Integer = intervalMs - CInt(sw.ElapsedMilliseconds)
            If remaining > 1 Then Thread.Sleep(remaining)
        Loop
    End Sub

    ''' <summary>MatをBGR24(CV_8UC3)に変換して返す。変換不要な場合は同じ参照を返す。</summary>
    Private Shared Function ToBgr24(mat As Mat) As Mat
        Try
            Dim t As MatType = mat.Type()
            If t = MatType.CV_8UC3 Then
                Return mat  ' すでにBGR24
            ElseIf t = MatType.CV_8UC4 Then
                Dim dst As New Mat()
                Cv2.CvtColor(mat, dst, ColorConversionCodes.BGRA2BGR)
                Return dst
            ElseIf t = MatType.CV_8UC1 Then
                Dim dst As New Mat()
                Cv2.CvtColor(mat, dst, ColorConversionCodes.GRAY2BGR)
                Return dst
            ElseIf t = MatType.CV_16UC3 Then
                Dim dst As New Mat()
                mat.ConvertTo(dst, MatType.CV_8UC3, 1.0 / 256)
                Return dst
            Else
                Return mat  ' 未知のフォーマット: そのまま試みる
            End If
        Catch
            Return Nothing
        End Try
    End Function

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

            RaiseEvent CameraError(Me, $"ffmpeg起動: {ffmpegExe} {args}")

            Dim psi As New System.Diagnostics.ProcessStartInfo(ffmpegExe, args)
            psi.UseShellExecute = False
            psi.RedirectStandardInput = True
            psi.RedirectStandardError = True
            psi.CreateNoWindow = True
            psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden

            _ffmpegProcess = System.Diagnostics.Process.Start(psi)
            If _ffmpegProcess Is Nothing Then
                RaiseEvent CameraError(Me, "ffmpegプロセス起動失敗（Process.Start が Nothing を返した）")
                Return False
            End If

            ' stderrを読み捨て（デッドロック防止）
            System.Threading.Tasks.Task.Run(Sub()
                Try
                    Dim errOut = _ffmpegProcess.StandardError.ReadToEnd()
                    If errOut.Length > 0 Then
                        RaiseEvent CameraError(Me, "ffmpeg stderr: " & errOut.Substring(0, Math.Min(200, errOut.Length)))
                    End If
                Catch
                End Try
            End Sub)

            _ffmpegStream = _ffmpegProcess.StandardInput.BaseStream
            _isRecording = True
            Return True
        Catch ex As Exception
            RaiseEvent CameraError(Me, "録画開始エラー: " & ex.Message & " / " & ex.GetType().Name)
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
        StopRecording()
        ' スレッド終了を待つ（最大2秒）
        If _captureThread IsNot Nothing Then
            _captureThread.Join(2000)
            _captureThread = Nothing
        End If
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
