Imports System.IO
Imports System.Threading.Tasks

''' <summary>
''' FFMpegCoreを使った動画・音声合成モジュール
''' OpenCVのVideoWriterへの依存をゼロにし、ffmpeg.exeをPure Managed方式で制御する。
'''
''' 動作:
'''   1. カメラ映像フレームをffmpegにパイプ → 映像のみのmp4を生成
'''   2. NAudioで録音したwavファイルと映像mp4をffmpegで合成 → 最終mp4を生成
'''   3. 一時ファイルは合成後に削除
'''
''' FFMpegCoreの利点:
'''   - NuGetで管理されるためDLLの手動配置不要
'''   - ffmpeg.exeはアプリと同じフォルダに配置（またはFFMpeg.Downloaderで自動取得）
'''   - .NET Framework 4.8 でも動作
''' </summary>
Public Class VideoMerger

    Private Shared _ffmpegExe As String = ""

    ''' <summary>
    ''' ffmpeg.exe のパスを初期化する。
    ''' アプリケーションの実行フォルダを最初に探し、なければPATH環境変数を探す。
    ''' </summary>
    Public Shared Function Initialize() As Boolean
        ' 1. アプリと同じフォルダを確認
        Dim localPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe")
        If File.Exists(localPath) Then
            _ffmpegExe = localPath
            Return True
        End If

        ' 2. PATH環境変数を検索
        Dim pathEnv As String = Environment.GetEnvironmentVariable("PATH") ?? ""
        For Each dir As String In pathEnv.Split(";")
            Dim candidate As String = Path.Combine(dir.Trim(), "ffmpeg.exe")
            If File.Exists(candidate) Then
                _ffmpegExe = candidate
                Return True
            End If
        Next

        Return False
    End Function

    ''' <summary>ffmpeg.exe が使用可能かどうか</summary>
    Public Shared ReadOnly Property IsAvailable As Boolean
        Get
            If _ffmpegExe <> "" Then Return True
            Return Initialize()
        End Get
    End Property

    ''' <summary>ffmpeg.exe のパスを返す</summary>
    Public Shared ReadOnly Property FfmpegPath As String
        Get
            If _ffmpegExe = "" Then Initialize()
            Return _ffmpegExe
        End Get
    End Property

    ''' <summary>
    ''' 映像ファイルと音声ファイルを合成して最終ファイルを生成する。
    ''' ffmpegに「-c:v copy」を使うため、再エンコードなしで高速に合成できる。
    ''' </summary>
    ''' <param name="videoFile">映像のみのmp4ファイルパス</param>
    ''' <param name="audioFile">音声wavファイルパス</param>
    ''' <param name="outputFile">出力mp4ファイルパス</param>
    ''' <returns>成功したらTrue</returns>
    Public Shared Function MergeVideoAudio(videoFile As String, audioFile As String, outputFile As String) As Boolean
        If Not IsAvailable Then
            Throw New FileNotFoundException("ffmpeg.exe が見つかりません。アプリケーションフォルダに配置してください。")
        End If

        Try
            ' 既に出力ファイルが存在する場合は削除
            If File.Exists(outputFile) Then File.Delete(outputFile)

            ' ffmpegで映像と音声を合成（再エンコードなし）
            Dim args As String = String.Format(
                "-y -i ""{0}"" -i ""{1}"" -c:v copy -c:a aac -map 0:v:0 -map 1:a:0 ""{2}""",
                videoFile, audioFile, outputFile)

            Dim success As Boolean = RunFfmpeg(args, 60000) ' 60秒タイムアウト
            Return success
        Catch ex As Exception
            Throw New Exception("映像・音声の合成に失敗しました: " & ex.Message, ex)
        End Try
    End Function

    ''' <summary>
    ''' 映像ファイルのみで出力する（音声なし）。
    ''' 音声録音に失敗した場合のフォールバック。
    ''' </summary>
    Public Shared Function CopyVideoOnly(videoFile As String, outputFile As String) As Boolean
        If Not IsAvailable Then Return False

        If File.Exists(outputFile) Then File.Delete(outputFile)
        Dim args As String = String.Format("-y -i ""{0}"" -c:v copy ""{1}""", videoFile, outputFile)
        Return RunFfmpeg(args, 60000)
    End Function

    ''' <summary>ffmpegを非同期で実行し完了を待つ</summary>
    Public Shared Async Function MergeVideoAudioAsync(videoFile As String, audioFile As String, outputFile As String) As Task(Of Boolean)
        Return Await Task.Run(Function() MergeVideoAudio(videoFile, audioFile, outputFile))
    End Function

    ''' <summary>ffmpegプロセスを同期実行する（内部ヘルパー）</summary>
    Private Shared Function RunFfmpeg(arguments As String, timeoutMs As Integer) As Boolean
        Try
            Dim psi As New System.Diagnostics.ProcessStartInfo(_ffmpegExe, arguments)
            psi.UseShellExecute = False
            psi.CreateNoWindow = True
            psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            psi.RedirectStandardError = True

            Using proc As System.Diagnostics.Process = System.Diagnostics.Process.Start(psi)
                ' stderrを非同期で読み捨て（デッドロック防止）
                Dim errorOutput As String = ""
                Dim t As Task = Task.Run(Sub() errorOutput = proc.StandardError.ReadToEnd())

                Dim exited As Boolean = proc.WaitForExit(timeoutMs)
                If Not exited Then
                    Try
                        proc.Kill()
                    Catch
                    End Try
                    Return False
                End If

                t.Wait(5000)
                Return proc.ExitCode = 0
            End Using
        Catch ex As Exception
            Throw New Exception("ffmpegの実行に失敗しました: " & ex.Message, ex)
        End Try
    End Function

    ''' <summary>
    ''' 一時ファイルを安全に削除する。
    ''' ffmpegプロセスが完全に終了してから削除を試みる。
    ''' </summary>
    Public Shared Sub DeleteTempFile(filePath As String)
        If Not File.Exists(filePath) Then Exit Sub
        Dim attempts As Integer = 0
        Do While attempts < 5
            Try
                File.Delete(filePath)
                Exit Do
            Catch
                Threading.Thread.Sleep(500)
                attempts += 1
            End Try
        Loop
    End Sub

End Class
