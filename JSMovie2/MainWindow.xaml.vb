Imports System.IO
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Input
Imports System.Windows.Threading
Imports System.Windows.Media
Imports System.Threading.Tasks
Imports System.Collections.ObjectModel

' 音声部分 (NAudio 2.x - WaveInEvent は NAudio.Wave 名前空間)
Imports NAudio.Wave

Class MainWindow

    '====== カメラ録画関連 ======
    ' OpenCvSharpを使わず CameraCapture クラス経由でカメラにアクセスする
    Private WithEvents _camera As CameraCapture
    Private _timer表示 As DispatcherTimer   ' 録画タイマー表示用

    '====== 音声録音関連 ======
    Private _isRecording As Boolean = False
    Private _waveSource As WaveInEvent      ' NAudio 2.x では WaveInEvent を推奨
    Private _waveFile As WaveFileWriter

    '====== ファイルパス関連 ======
    Private _tempVideoFile As String = "TempVideo.mp4"  ' ffmpegが書き込む一時映像ファイル
    Private _tempAudioFile As String = "Temp.wav"
    Private _outputFile As String
    Private _recordingFolderPath As String

    '====== 競技会情報 ======
    Private _競技会NO As String
    Private _競技会名 As String

    '====== 設定 ======
    Private _システム設定 As システム設定ファイル
    Private _log As LOG_C

    '====== 録画タイマー ======
    Private _録画開始時刻 As DateTime
    Private _録画中FLAG As Boolean = False
    Private _timer録画表示 As DispatcherTimer
    Private _TimerStartTime As DateTime
    Private _TimerStatus As String = "N"

    '====== 再生関連 ======
    Private _videoPath As String
    Private _timer再生 As DispatcherTimer
    Private _timerInterval As Double
    Private _elapsedSec As Double = 0
    Private _sliderValueChangedByProgram As Boolean = False
    Private _fps As Integer = 30
    Private _pauseFlag As Boolean = False
    Private _start As System.Drawing.Point?
    Private _origin As System.Drawing.Point?
    Private _counter As Integer

    '====== 再生リスト関連 ======
    Private _videohomePath As String = ".\Data"
    Private _f_Index As F_Index
    Private _現在_選択競技会FullName As String
    Private _現在_選択区分NO As String
    Private _現在_選択ラウンドNO As String

    '====== 通信関連 ======
    Private WithEvents _通信Main As 通信Main_C
    Private _ジャッジ記号() As String

    ' カメラ番号
    Private _カメラ番号 As Integer = 0

    Sub New()
        _log = New LOG_C()
        _log.SetLogLevel(5)
        _log.Set_ON()
        _log.CreateFile()

        _システム設定 = New システム設定ファイル()

        If _システム設定.VideoPath <> "" Then
            _videohomePath = _システム設定.VideoPath
        End If

        _カメラ番号 = _システム設定.カメラ番号

        ' XAMLを初期化 (InitializeComponentを先に呼ぶ必要がある)
        InitializeComponent()

        ' ffmpegの確認
        If Not VideoMerger.Initialize() Then
            MessageBox.Show(
                "ffmpeg.exe が見つかりません。" & vbCrLf &
                "アプリケーションフォルダに ffmpeg.exe を配置してください。" & vbCrLf &
                vbCrLf & "ダウンロード先: https://ffmpeg.org/download.html",
                "ffmpegが見つかりません",
                MessageBoxButton.OK, MessageBoxImage.Warning)
        End If
    End Sub

    Private Async Sub ContentRenderedEvent(sender As Object, e As EventArgs) Handles Me.ContentRendered
        画面切り替え("R")

        ' カメラ選択ダイアログ
        Dim 選択画面 As New カメラ選択()
        If _カメラ番号 = 1 Then
            選択画面.RB_01.IsChecked = True
        ElseIf _カメラ番号 = 2 Then
            選択画面.RB_02.IsChecked = True
        Else
            選択画面.RB_00.IsChecked = True
        End If

        Dim result As Boolean? = 選択画面.ShowDialog()
        _カメラ番号 = 選択画面.SelectedCameraIndex

        ' カメラ起動（非同期）
        Await カメラ起動Async()

        再生リスト更新()
    End Sub

    '======================================================
    '   カメラ関連 (新実装 - OpenCvSharp不使用)
    '======================================================

    Private Async Function カメラ起動Async() As Task
        Try
            Dim 起動画面 As New 起動中画面()
            起動画面.Show()

            If _camera IsNot Nothing Then
                _camera.Dispose()
                _camera = Nothing
            End If

            _camera = New CameraCapture(_カメラ番号)
            _camera.FrameWidth = 1440
            _camera.FrameHeight = 810
            _camera.Fps = 30

            ' 非同期でカメラ起動（UIスレッドをブロックしない）
            Dim success As Boolean = Await _camera.StartAsync(Img_Screen)

            起動画面.Hide()
            起動画面.Close()

            If Not success Then
                MessageBox.Show(
                    "カメラを使用できません。" & vbCrLf &
                    "カメラ機能は無効ですが、アプリケーションは続行します。" & vbCrLf & vbCrLf &
                    "確認事項:" & vbCrLf &
                    "1. Windowsの設定 → プライバシー → カメラ でアクセス許可を確認" & vbCrLf &
                    "2. 他のアプリ(Zoom, Teams等)でカメラが使用されていないか確認" & vbCrLf &
                    "3. デバイスマネージャーでカメラが正常に動作しているか確認",
                    "カメラ警告", MessageBoxButton.OK, MessageBoxImage.Warning)
            End If
        Catch ex As Exception
            MessageBox.Show("カメラ起動エラー: " & ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Function

    Private Sub Camera_CameraError(sender As Object, message As String) Handles _camera.CameraError
        _log.LogAdd("カメラエラー: " & message, _log.ERR)
    End Sub

    '======================================================
    '   録画関連
    '======================================================

    Private Sub 録画スタート(path_ As String, outputFile_ As String)
        If _録画中FLAG Then Exit Sub

        _録画中FLAG = True
        PB_録画開始.Content = "録画停止"

        _recordingFolderPath = path_
        _outputFile = outputFile_

        ' フォルダー設定
        If _recordingFolderPath = "" Then
            If _競技会NO = "" Then
                _recordingFolderPath = _videohomePath
            Else
                _recordingFolderPath = Path.Combine(_videohomePath, _競技会NO & "_" & _競技会名)
            End If
        End If

        If Not Directory.Exists(_recordingFolderPath) Then
            Directory.CreateDirectory(_recordingFolderPath)
        End If

        If _outputFile = "" Then
            Dim 連番 As String = Get_ファイル名連番(_recordingFolderPath, "Movie")
            _outputFile = "Movie_" & 連番 & ".mp4"
        End If

        ' ffmpegへのパイプで映像録画開始
        Dim videoTempPath As String = Path.Combine(_recordingFolderPath, _tempVideoFile)
        If _camera IsNot Nothing Then
            _camera.StartRecording(videoTempPath)
        End If

        ' 音声録音開始 (NAudio 2.x)
        Dim audioTempPath As String = Path.Combine(_recordingFolderPath, _tempAudioFile)
        StartAudioRecording(audioTempPath)

        ' 録画タイマー表示
        タイマ初期設定()
        StartカウントUP()

        ' Recording表示
        Me.LB_Status.Visibility = Visibility.Collapsed
    End Sub

    Private Sub 録画終了()
        If Not _録画中FLAG Then Exit Sub

        _録画中FLAG = False
        PB_録画開始.Content = "録画開始"
        LB_FPS.Content = ""

        ' 映像録画停止
        If _camera IsNot Nothing Then
            _camera.StopRecording()
        End If

        ' 音声録音停止
        StopAudioRecording()

        ' タイマー停止
        Stopタイマー()
        Me.LB_Status.Visibility = Visibility.Hidden

        ' ffmpegで映像と音声を合成（非同期）
        Dim videoPath = Path.Combine(_recordingFolderPath, _tempVideoFile)
        Dim audioPath = Path.Combine(_recordingFolderPath, _tempAudioFile)
        Dim outputPath = Path.Combine(_recordingFolderPath, _outputFile)

        Task.Run(Async Function()
                     Try
                         System.Threading.Thread.Sleep(1000) ' ファイルが完全に書き込まれるまで待機

                         If File.Exists(audioPath) AndAlso File.Exists(videoPath) Then
                             VideoMerger.MergeVideoAudio(videoPath, audioPath, outputPath)
                         ElseIf File.Exists(videoPath) Then
                             VideoMerger.CopyVideoOnly(videoPath, outputPath)
                         End If

                         ' 一時ファイルを削除
                         VideoMerger.DeleteTempFile(videoPath)
                         VideoMerger.DeleteTempFile(audioPath)
                     Catch ex As Exception
                         _log.LogAdd("合成エラー: " & ex.Message, _log.ERR)
                     End Try

                     ' UI更新はDispatcher経由
                     Await Dispatcher.InvokeAsync(Async Function()
                                                      Await カメラ起動Async()
                                                      If _現在_選択競技会FullName <> "" AndAlso _現在_選択区分NO <> "" AndAlso _現在_選択ラウンドNO <> "" Then
                                                          ファイルボタン作成(_現在_選択競技会FullName, _現在_選択区分NO, _現在_選択ラウンドNO)
                                                      Else
                                                          再生リスト更新()
                                                      End If
                                                  End Function)
                 End Function)
    End Sub

    Private Sub PB_録画開始_Click(sender As Object, e As RoutedEventArgs) Handles PB_録画開始.Click
        If Not _録画中FLAG Then
            録画スタート("", "")
        Else
            録画終了()
        End If
    End Sub

    '======================================================
    '   音声録音関連 (NAudio 2.x)
    '======================================================

    Private Sub StartAudioRecording(filePath As String)
        Try
            _isRecording = True
            _waveSource = New WaveInEvent()
            _waveSource.WaveFormat = New WaveFormat(44100, 1)
            AddHandler _waveSource.DataAvailable, AddressOf WaveSource_DataAvailable
            AddHandler _waveSource.RecordingStopped, AddressOf WaveSource_RecordingStopped
            _waveFile = New WaveFileWriter(filePath, _waveSource.WaveFormat)
            _waveSource.StartRecording()
        Catch ex As Exception
            _log.LogAdd("音声録音開始エラー: " & ex.Message, _log.ERR)
        End Try
    End Sub

    Private Sub StopAudioRecording()
        _isRecording = False
        If _waveSource IsNot Nothing Then
            _waveSource.StopRecording()
        End If
    End Sub

    Private Sub WaveSource_DataAvailable(sender As Object, e As WaveInEventArgs)
        If _waveFile IsNot Nothing Then
            _waveFile.Write(e.Buffer, 0, e.BytesRecorded)
            _waveFile.Flush()
        End If
    End Sub

    Private Sub WaveSource_RecordingStopped(sender As Object, e As StoppedEventArgs)
        If _waveSource IsNot Nothing Then
            _waveSource.Dispose()
            _waveSource = Nothing
        End If
        If _waveFile IsNot Nothing Then
            _waveFile.Dispose()
            _waveFile = Nothing
        End If
    End Sub

    '======================================================
    '   タイマー処理
    '======================================================

    Delegate Sub TimerUpdateDelegate(ByVal str As String, カラー As System.Windows.Media.Brush)
    Private _timerDelegate As New TimerUpdateDelegate(AddressOf Timer更新)

    Private Sub タイマ初期設定()
        _timer録画表示 = New DispatcherTimer()
        AddHandler _timer録画表示.Tick, New EventHandler(AddressOf timer表示_Tick)
        _TimerStatus = "N"
        Timer更新("00:00", System.Windows.Media.Brushes.Cyan)
        _timer録画表示.Interval = New TimeSpan(0, 0, 1)
        _timer録画表示.Start()
    End Sub

    Private Sub StartカウントUP()
        _timer録画表示.Start()
        _TimerStartTime = DateTime.Now
        Timer更新("00:00", System.Windows.Media.Brushes.Aqua)
        _TimerStatus = "R"
    End Sub

    Private Sub Stopタイマー()
        Timer更新("00:00", System.Windows.Media.Brushes.Cyan)
        _TimerStatus = "N"
    End Sub

    Private Sub Timer更新(str As String, カラー As System.Windows.Media.Brush)
        Me.LB_タイマー.Content = str
        Me.LB_タイマー.Background = カラー
    End Sub

    Private Sub timer表示_Tick(sender As Object, e As EventArgs)
        If _TimerStatus = "R" Then
            Dim diff = DateTime.Now - _TimerStartTime
            Dim diff2 = Format(diff.Minutes, "00") & ":" & Format(diff.Seconds, "00")
            Timer更新("R " & diff2, System.Windows.Media.Brushes.PeachPuff)
        End If
    End Sub

    '======================================================
    '   再生リスト関連
    '======================================================

    Private Sub PB_Refresh_Click(sender As Object, e As RoutedEventArgs) Handles PB_Refresh.Click
        再生リスト更新()
    End Sub

    Private Sub 再生リスト更新()
        CB競技会更新()
    End Sub

    Private Sub CB競技会更新()
        CB_競技会.Items.Clear()

        If Not Directory.Exists(_videohomePath) Then
            MessageBox.Show(_videohomePath & " は存在しません。設定ファイルを修正してください。",
                "フォルダエラー", MessageBoxButton.OK, MessageBoxImage.Error)
            Application.Current.Shutdown()
            Exit Sub
        End If

        Dim 初期INDEX As Integer = 0
        Dim dirs() As String = Directory.GetDirectories(_videohomePath)
        For Each dir As String In dirs
            Dim DirName As String = dir.Replace(_videohomePath & "\", "")
            CB_競技会.Items.Add(DirName)
            If DirName = _現在_選択競技会FullName Then
                初期INDEX = CB_競技会.Items.Count
            End If
        Next

        If 初期INDEX > 0 Then CB_競技会.SelectedIndex = 初期INDEX - 1
    End Sub

    Private Sub CB_競技会_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Dim 選択競技会FullName As String = CB_競技会.SelectedValue
        If 選択競技会FullName IsNot Nothing Then
            CB区分更新(選択競技会FullName)
        End If
    End Sub

    Private Sub Indexファイルの読み込み(Path As String)
        _f_Index = New F_Index(Path)
    End Sub

    Private Sub CB区分更新(選択競技会FullName As String)
        Indexファイルの読み込み(Path.Combine(_videohomePath, 選択競技会FullName))
        If _f_Index.登録済みレコード数 = 0 Then Exit Sub

        Dim 区分NO一覧(100) As String
        Dim 区分名一覧(100) As String
        Dim 登録済み区分数 As Integer = 0
        Dim 初期INDEX As Integer = 0

        For i = 1 To _f_Index.登録済みレコード数
            If _f_Index.リスト(i).区分NO <> "" Then
                Dim found As Boolean = False
                For k = 1 To 登録済み区分数
                    If 区分NO一覧(k) = _f_Index.リスト(i).区分NO Then found = True : k = 登録済み区分数
                Next k
                If Not found Then
                    登録済み区分数 += 1
                    区分NO一覧(登録済み区分数) = _f_Index.リスト(i).区分NO
                    区分名一覧(登録済み区分数) = _f_Index.リスト(i).区分名
                End If
            End If
        Next i

        CB_区分.Items.Clear()
        For k = 1 To 登録済み区分数
            CB_区分.Items.Add(区分NO一覧(k) & "_" & 区分名一覧(k))
            If _現在_選択区分NO = 区分NO一覧(k) Then 初期INDEX = CB_区分.Items.Count
        Next k

        If 初期INDEX > 0 Then CB_区分.SelectedIndex = 初期INDEX - 1
    End Sub

    Private Sub CB_区分_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Dim 選択競技会FullName As String = CB_競技会.SelectedValue
        Dim 選択区分FullName As String = CB_区分.SelectedValue
        If 選択区分FullName IsNot Nothing Then
            Dim 選択区分NO As String = If(選択区分FullName.Contains("_"), 選択区分FullName.Split("_")(0), "")
            CBラウンド更新(選択競技会FullName, 選択区分NO)
        End If
    End Sub

    Private Sub CBラウンド更新(選択競技会FullName As String, 選択区分NO As String)
        Indexファイルの読み込み(Path.Combine(_videohomePath, 選択競技会FullName))
        If _f_Index.登録済みレコード数 = 0 Then Exit Sub

        Dim ラウンドNO一覧(100) As String
        Dim ラウンド名一覧(100) As String
        Dim 登録済み区分数 As Integer = 0
        Dim 初期INDEX As Integer = 0

        For i = 1 To _f_Index.登録済みレコード数
            If _f_Index.リスト(i).区分NO <> "" Then
                Dim found As Boolean = False
                For k = 1 To 登録済み区分数
                    If 選択区分NO = _f_Index.リスト(i).区分NO AndAlso ラウンドNO一覧(k) = _f_Index.リスト(i).ラウンドNO Then
                        found = True : k = 登録済み区分数
                    End If
                Next k
                If Not found Then
                    登録済み区分数 += 1
                    ラウンドNO一覧(登録済み区分数) = _f_Index.リスト(i).ラウンドNO
                    ラウンド名一覧(登録済み区分数) = _f_Index.リスト(i).ラウンド名
                End If
            End If
        Next i

        CB_ラウンド.Items.Clear()
        For k = 1 To 登録済み区分数
            CB_ラウンド.Items.Add(ラウンドNO一覧(k) & "_" & ラウンド名一覧(k))
            If _現在_選択ラウンドNO = ラウンドNO一覧(k) Then 初期INDEX = CB_ラウンド.Items.Count
        Next k

        If 初期INDEX > 0 Then CB_ラウンド.SelectedIndex = 初期INDEX - 1
    End Sub

    Private Sub CB_ラウンド_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Dim 選択競技会FullName As String = CB_競技会.SelectedValue
        Dim 選択区分FullName As String = CB_区分.SelectedValue
        Dim 選択区分NO As String = ""
        Dim 選択ラウンドNO As String = ""

        If CB_ラウンド.SelectedValue IsNot Nothing Then
            Dim 選択ラウンドFullName As String = CB_ラウンド.SelectedValue
            If 選択区分FullName IsNot Nothing AndAlso 選択区分FullName.Contains("_") Then
                選択区分NO = 選択区分FullName.Split("_")(0)
            End If
            If 選択ラウンドFullName IsNot Nothing AndAlso 選択ラウンドFullName.Contains("_") Then
                選択ラウンドNO = 選択ラウンドFullName.Split("_")(0)
            End If
        Else
            選択区分NO = _現在_選択区分NO
            選択ラウンドNO = _現在_選択ラウンドNO
        End If

        ファイルボタン作成(選択競技会FullName, 選択区分NO, 選択ラウンドNO)
        _現在_選択競技会FullName = 選択競技会FullName
        _現在_選択区分NO = 選択区分NO
        _現在_選択ラウンドNO = 選択ラウンドNO
    End Sub

    Private Function ファイル一覧の作成(選択競技会FullName As String, 区分NO As String, ラウンドNO As String) As F_Index.FD_ファイル詳細()
        Dim folderPath As String = Path.Combine(_videohomePath, 選択競技会FullName)
        If Not Directory.Exists(folderPath) Then Return New F_Index.FD_ファイル詳細() {}

        Dim files() As String = Directory.GetFiles(folderPath)
        Array.Sort(Of String)(files, AddressOf CompareLastWriteTime)

        Dim 表示ファイル一覧(0) As F_Index.FD_ファイル詳細
        Dim 表示ファイル数 As Integer = 0

        Indexファイルの読み込み(folderPath)

        For Each file As String In files
            If Path.GetExtension(file).ToLower() = ".mp4" Then
                Dim fd As F_Index.FD_ファイル詳細 = _f_Index.Get_FDファイル詳細(Path.GetFileName(file))

                If fd Is Nothing Then
                    ReDim Preserve 表示ファイル一覧(表示ファイル数 + 1)
                    表示ファイル一覧(表示ファイル数 + 1) = New F_Index.FD_ファイル詳細 With {.ファイル名 = Path.GetFileName(file)}
                    表示ファイル数 += 1
                Else
                    Dim match As Boolean = True
                    If 区分NO <> "" Then
                        match = (fd.区分NO = 区分NO)
                        If match AndAlso ラウンドNO <> "" Then
                            match = (fd.ラウンドNO = ラウンドNO)
                        End If
                    End If
                    If match Then
                        ReDim Preserve 表示ファイル一覧(表示ファイル数 + 1)
                        表示ファイル一覧(表示ファイル数 + 1) = New F_Index.FD_ファイル詳細
                        表示ファイル数 += 1
                        表示ファイル一覧(表示ファイル数).登録(fd)
                    End If
                End If
            End If
        Next

        Return 表示ファイル一覧
    End Function

    Shared Function CompareLastWriteTime(fileX As String, fileY As String) As Integer
        Return DateTime.Compare(File.GetLastWriteTime(fileY), File.GetLastWriteTime(fileX))
    End Function

    Private Sub ファイルボタン作成(選択競技会FullName As String, 区分NO As String, ラウンドNO As String)
        ボタンクリア()
        Dim 表示ファイル一覧 As F_Index.FD_ファイル詳細() = ファイル一覧の作成(選択競技会FullName, 区分NO, ラウンドNO)

        For Each fd As F_Index.FD_ファイル詳細 In 表示ファイル一覧
            If fd IsNot Nothing Then
                Dim btn As New Button()
                btn.Tag = Path.Combine(_videohomePath, 選択競技会FullName, fd.ファイル名)
                Dim tb As New TextBlock()
                If fd.区分NO <> "" Then
                    tb.Text = fd.区分名 & " " & fd.ラウンド名 & vbCrLf & fd.種目名 & " " & fd.ヒート番号 & " " & fd.選手名
                Else
                    tb.Text = fd.ファイル名
                End If
                tb.TextWrapping = TextWrapping.WrapWithOverflow
                btn.Content = tb
                btn.Height = 50
                btn.Width = uniformGrid2.Width
                AddHandler btn.Click, AddressOf FileBtn_Click
                uniformGrid2.Children.Add(btn)
            End If
        Next
    End Sub

    Private Sub ボタンクリア()
        While uniformGrid2.Children.Count > 0
            uniformGrid2.Children.Remove(uniformGrid2.Children(0))
        End While
    End Sub

    Private Sub FileBtn_Click(sender As Object, e As RoutedEventArgs)
        Dim btn = DirectCast(sender, Button)
        Dim path As String = CStr(btn.Tag)
        動画再生(path)
        SEND_REQBMLIST(path)
    End Sub

    '======================================================
    '   再生画面
    '======================================================

    Public Sub 動画再生(videoPath As String)
        画面切り替え("P")
        _videoPath = videoPath
        _timerInterval = 1.0 / _fps
        _timer再生 = New DispatcherTimer()
        _timer再生.Interval = TimeSpan.FromMilliseconds(_timerInterval * 1000)
        AddHandler _timer再生.Tick, New EventHandler(AddressOf dispatcherTimer_Tick)
        _pauseFlag = False
        _start = Nothing
        _origin = Nothing
        _counter = 0
        画面サイズを元サイズにする()
        動画再生スタート()
    End Sub

    Private Sub 画面切り替え(PorR As String)
        If PorR = "P" Then
            Me.SCV_Screen.Visibility = Visibility.Hidden
            Me.SCV_REC_Control.Visibility = Visibility.Hidden
            Me.LB_FPS.Visibility = Visibility.Hidden
            Me.SCV_Screen_Play.Visibility = Visibility.Visible
            Me.Grid_Play_SCBar.Visibility = Visibility.Visible
            Me.SCV_PLAY_Control.Visibility = Visibility.Visible
            Me.Grid_Hedder.Background = System.Windows.Media.Brushes.Blue

            ' カメラ表示停止
            If _camera IsNot Nothing Then
                _camera.StopRecording()
            End If
        Else
            Me.SCV_Screen.Visibility = Visibility.Visible
            Me.SCV_REC_Control.Visibility = Visibility.Visible
            Me.LB_FPS.Visibility = Visibility.Visible
            Me.SCV_Screen_Play.Visibility = Visibility.Hidden
            Me.Grid_Play_SCBar.Visibility = Visibility.Hidden
            Me.SCV_PLAY_Control.Visibility = Visibility.Hidden
            Me.Grid_Hedder.Background = System.Windows.Media.Brushes.Black
            Dispatcher.Invoke(Sub() BMボタンクリア())
        End If
    End Sub

    Private Sub dispatcherTimer_Tick(sender As Object, e As EventArgs)
        If MediaElementMovie.NaturalDuration.HasTimeSpan Then
            _sliderValueChangedByProgram = True
            Dim 現在TimeSpan As TimeSpan = MediaElementMovie.Position
            Dim totalSec As Double = MediaElementMovie.NaturalDuration.TimeSpan.TotalSeconds
            Dim 現在秒数 As Double = 現在TimeSpan.TotalSeconds

            SliderMoviePosition.Value = (現在秒数 / totalSec) * SliderMoviePosition.Maximum
            Me.LB_Total時間.Content = totalSec.ToString("0.0")
            Me.LB_経過時間.Content = 現在秒数.ToString("0.0")
        End If
    End Sub

    Private Sub 動画再生スタート()
        MediaElementMovie.SpeedRatio = 1
        MediaElementMovie.ScrubbingEnabled = True
        Me.LB_Hedder.Content = "再生中 " & Path.GetFileName(_videoPath)

        If Not _pauseFlag Then
            ' 絶対パスで Uri を生成（UriKind.Absolute で確実に）
            Dim uri As Uri
            If System.IO.Path.IsPathRooted(_videoPath) Then
                uri = New Uri(_videoPath, UriKind.Absolute)
            Else
                uri = New Uri(System.IO.Path.GetFullPath(_videoPath), UriKind.Absolute)
            End If
            MediaElementMovie.Source = uri
            画面サイズを元サイズにする()
        End If

        _timer再生.Start()
        MediaElementMovie.Play()
    End Sub

    Private Sub ButtonPlay_Click(sender As Object, e As RoutedEventArgs)
        動画再生スタート()
    End Sub

    Private Sub ButtonPause_Click(sender As Object, e As RoutedEventArgs)
        _timer再生.Stop()
        MediaElementMovie.Pause()
        _pauseFlag = True
    End Sub

    Private Sub ButtonStop_Click(sender As Object, e As RoutedEventArgs)
        _timer再生.Stop()
        _elapsedSec = 0
        SliderMoviePosition.Value = 0
        MediaElementMovie.Stop()
        MediaElementMovie.Source = Nothing
        Me.LB_Hedder.Content = ""
        画面切り替え("R")
    End Sub

    Private Sub ButtonSlow_Click(sender As Object, e As RoutedEventArgs)
        MediaElementMovie.SpeedRatio = 0.5
    End Sub

    Private Sub Button早送り_Click(sender As Object, e As RoutedEventArgs)
        MediaElementMovie.SpeedRatio = 2
    End Sub

    Private Sub Buttonコマ送り_Click(sender As Object, e As RoutedEventArgs)
        Dim ts As TimeSpan = MediaElementMovie.Position
        MediaElementMovie.Position = ts.Add(TimeSpan.FromMilliseconds(_fps))
        _timer再生.Start()
    End Sub

    Private Sub Buttonコマ戻り_Click(sender As Object, e As RoutedEventArgs)
        Dim ts As TimeSpan = MediaElementMovie.Position
        Dim newMs As Double = Math.Max(0, ts.TotalMilliseconds - _fps)
        MediaElementMovie.Position = TimeSpan.FromMilliseconds(newMs)
        _timer再生.Start()
    End Sub

    Private Sub SliderMoviePosition_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If Not _sliderValueChangedByProgram Then
            If MediaElementMovie.NaturalDuration.HasTimeSpan Then
                Dim totalSec As Double = MediaElementMovie.NaturalDuration.TimeSpan.TotalSeconds
                Dim targetSec As Integer = CInt(SliderMoviePosition.Value * totalSec / SliderMoviePosition.Maximum)
                _elapsedSec = targetSec
                MediaElementMovie.Position = TimeSpan.FromSeconds(targetSec)
            End If
        End If
        _sliderValueChangedByProgram = False
    End Sub

    Private Sub ButtonORGSize_Click(sender As Object, e As RoutedEventArgs)
        画面サイズを元サイズにする()
    End Sub

    Private Sub 画面サイズを元サイズにする()
        Dim SC_Size_x As Double = SCV_Screen_Play.RenderSize.Width
        Dim SC_Size_y As Double = SCV_Screen_Play.RenderSize.Height

        Dim 動画高さ As Double = MediaElementMovie.NaturalVideoHeight
        Dim 動画幅 As Double = MediaElementMovie.NaturalVideoWidth
        Dim 動画縦横比 As Double = If(動画高さ > 0, 動画幅 / 動画高さ, 1920.0 / 1080.0)

        Dim tfg = New TransformGroup()
        tfg.Children.Add(New ScaleTransform(1, 1))
        tfg.Children.Add(New TranslateTransform(0, 0))
        MediaElementMovie.RenderTransform = tfg

        _origin = New System.Drawing.Point(CInt(MediaElementMovie.RenderTransform.Value.OffsetX),
                                           CInt(MediaElementMovie.RenderTransform.Value.OffsetY))
        MediaElementMovie.Height = SC_Size_y
        MediaElementMovie.Width = SC_Size_y * 動画縦横比
    End Sub

    Private Sub MediaElementMovie_MouseWheel(sender As Object, e As MouseWheelEventArgs)
        Dim zoom As Double = If(e.Delta > 0, 0.1, -0.1)
        Dim x As Double = MediaElementMovie.RenderTransform.Value.M11
        Dim tfg = New TransformGroup()
        tfg.Children.Add(New ScaleTransform(x + zoom, x + zoom))
        Dim ox As Double = If(_origin.HasValue, _origin.Value.X, 0)
        Dim oy As Double = If(_origin.HasValue, _origin.Value.Y, 0)
        tfg.Children.Add(New TranslateTransform(ox, oy))
        MediaElementMovie.RenderTransform = tfg
        _origin = New System.Drawing.Point(CInt(MediaElementMovie.RenderTransform.Value.OffsetX),
                                           CInt(MediaElementMovie.RenderTransform.Value.OffsetY))
    End Sub

    Private Sub MediaElementMovie_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        MediaElementMovie.CaptureMouse()
        Dim pos = e.GetPosition(Me.SCV_PLAY_Control)
        _start = New System.Drawing.Point(CInt(pos.X), CInt(pos.Y))
        _origin = New System.Drawing.Point(CInt(MediaElementMovie.RenderTransform.Value.OffsetX),
                                           CInt(MediaElementMovie.RenderTransform.Value.OffsetY))
        _counter = 0
    End Sub

    Private Sub MediaElementMovie_MouseMove(sender As Object, e As MouseEventArgs)
        If MediaElementMovie.IsMouseCaptured AndAlso _start.HasValue AndAlso _origin.HasValue Then
            Dim pos = e.GetPosition(Me.SCV_PLAY_Control)
            Dim dx As Double = pos.X - _start.Value.X
            Dim dy As Double = pos.Y - _start.Value.Y
            Dim xx As Double = MediaElementMovie.RenderTransform.Value.M11
            Dim yy As Double = MediaElementMovie.RenderTransform.Value.M22
            Dim tfg = New TransformGroup()

            If _counter = 0 Then
                tfg.Children.Add(New TranslateTransform(_origin.Value.X, _origin.Value.Y))
            Else
                tfg.Children.Add(New TranslateTransform((_origin.Value.X + dx) * (1 / xx),
                                                         (_origin.Value.Y + dy) * (1 / yy)))
            End If
            tfg.Children.Add(New ScaleTransform(xx, yy))
            MediaElementMovie.RenderTransform = tfg
            _counter += 1
        End If
    End Sub

    Private Sub MediaElementMovie_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs)
        MediaElementMovie.ReleaseMouseCapture()
        Dim pos = e.GetPosition(Me.SCV_PLAY_Control)
        _start = New System.Drawing.Point(CInt(pos.X), CInt(pos.Y))
        _origin = New System.Drawing.Point(CInt(MediaElementMovie.RenderTransform.Value.OffsetX),
                                           CInt(MediaElementMovie.RenderTransform.Value.OffsetY))
        _counter = 0
    End Sub

    '======================================================
    '   通信関連
    '======================================================

    Private Sub ReConnect(sender As Object, e As RoutedEventArgs)
        通信開始()
    End Sub

    Private Sub 通信開始()
        _通信Main = New 通信Main_C()
        _通信Main.main()
    End Sub

    Private Sub SEND_REQBMLIST(filepath As String)
        If _通信Main Is Nothing OrElse _通信Main.IsClosed() Then Exit Sub

        Dim 表示ファイル一覧 As F_Index.FD_ファイル詳細() = ファイル一覧の作成(_現在_選択競技会FullName, _現在_選択区分NO, _現在_選択ラウンドNO)
        Dim 種目記号 As String = ""
        Dim ヒート番号 As String = ""

        For Each fd As F_Index.FD_ファイル詳細 In 表示ファイル一覧
            If fd IsNot Nothing AndAlso filepath.Contains(fd.ファイル名) Then
                種目記号 = fd.種目記号
                ヒート番号 = CStr(fd.ヒート番号)
                Exit For
            End If
        Next

        If 種目記号 <> "" Then
            Dim denbun As String =
                "JS,REQBMLIST," & _システム設定.端末名 & ",1,1," &
                _現在_選択区分NO & "," &
                _現在_選択ラウンドNO & "," &
                種目記号 & "," &
                ヒート番号 & ","
            _通信Main.Send_REQ_BMLIST(denbun)
        End If
    End Sub

    Private Sub EVENT_SVR_Connected(ByVal sender As Object, ByVal e As EventArgs) Handles _通信Main.SVR_Connected
    End Sub

    Private Sub EVENT_SVR_DisConnected(ByVal sender As Object, ByVal e As EventArgs) Handles _通信Main.SVR_DisConnected
        Dispatcher.Invoke(Sub()
                              Me.TB_CompName.Text = "未接続"
                              Me.TB_CompName.Background = System.Windows.Media.Brushes.Pink
                          End Sub)
    End Sub

    Private Sub EVENT_RCV_KANS_MA_COMP(ByVal sender As Object, ByVal e As ReceivedDataEventArgs) Handles _通信Main.RCV_KANS_MA_COMP
        Dim str As String = e.ReceivedString
        Dim parts() As String = str.Split(",")
        If UBound(parts) >= 6 Then
            Dispatcher.Invoke(Sub()
                                  Me.TB_CompName.Text = parts(5) & vbCrLf & parts(6)
                                  Me.TB_CompName.Background = System.Windows.Media.Brushes.Cyan
                              End Sub)
            _競技会NO = parts(5)
            _競技会名 = parts(6)
        End If
    End Sub

    Private Sub EVENT_RCV_KANS_MB_KUBUN(ByVal sender As Object, ByVal e As ReceivedDataEventArgs) Handles _通信Main.RCV_KANS_MB_KUBUN
    End Sub

    Private Sub EVENT_RCV_KANS_MU_Progress(ByVal sender As Object, ByVal e As ReceivedDataEventArgs) Handles _通信Main.RCV_KANS_MU_Progress
    End Sub

    Private Sub EVENT_RCV_KANS_MOVIE_START(ByVal sender As Object, ByVal e As ReceivedDataEventArgs) Handles _通信Main.RCV_KANS_MOVIE_START
        Dim str As String = e.ReceivedString
        Dim MOVIE_START_J = New KANS_MOVIE_START_J()

        If UBound(str.Split(",")) >= 5 Then
            Dim 配列() As String = str.Split(",")
            Dim 削除文字列 As String = ""
            For i = 0 To 4
                削除文字列 &= 配列(i) & ","
            Next i
            Dim jsonStr As String = str.Replace(削除文字列, "")
            MOVIE_START_J = MOVIE_START_J.JSON読み込み(jsonStr)

            Dim filename As String = ""
            Dim 連番 As String

            Dim folderPath As String = Path.Combine(_videohomePath, _競技会NO & "_" & _競技会名)

            If MOVIE_START_J.SG種別 = "S" Then
                Dim safe名 = ファイル名安全化(Trim(MOVIE_START_J.リーダー名))
                filename = MOVIE_START_J.区分番号 & "_" & MOVIE_START_J.ラウンド番号 & "_" & MOVIE_START_J.種目記号 & "_" & MOVIE_START_J.ヒート番号 & "H_S_" & MOVIE_START_J.背番号 & "_" & safe名
                連番 = Get_ファイル名連番(folderPath, filename)
                filename &= "_" & 連番 & ".mp4"
                Dispatcher.Invoke(Sub() Me.LB_Hedder.Content = MOVIE_START_J.種目記号 & " " & MOVIE_START_J.ヒート番号 & "H Solo " & MOVIE_START_J.背番号 & " " & MOVIE_START_J.リーダー名)

            ElseIf MOVIE_START_J.SG種別 = "D" Then
                Dim safe名 = ファイル名安全化(Trim(MOVIE_START_J.リーダー名))
                filename = MOVIE_START_J.区分番号 & "_" & MOVIE_START_J.ラウンド番号 & "_" & MOVIE_START_J.種目記号 & "_" & MOVIE_START_J.ヒート番号 & "H_D_" & MOVIE_START_J.背番号 & "_" & safe名
                連番 = Get_ファイル名連番(folderPath, filename)
                filename &= "_" & 連番 & ".mp4"
                Dispatcher.Invoke(Sub() Me.LB_Hedder.Content = MOVIE_START_J.種目記号 & " " & MOVIE_START_J.ヒート番号 & "H Duel " & MOVIE_START_J.背番号 & " " & MOVIE_START_J.リーダー名)

            Else
                filename = MOVIE_START_J.区分番号 & "_" & MOVIE_START_J.ラウンド番号 & "_" & MOVIE_START_J.種目記号 & "_" & MOVIE_START_J.ヒート番号 & "H_" & MOVIE_START_J.SG種別
                連番 = Get_ファイル名連番(folderPath, filename)
                filename &= "_" & 連番 & ".mp4"
                Dispatcher.Invoke(Sub() Me.LB_Hedder.Content = MOVIE_START_J.種目記号 & " " & MOVIE_START_J.ヒート番号 & "H Group")
            End If

            ' indexファイルの登録
            Dim fi As New F_Index(folderPath)
            Dim fd As New F_Index.FD_ファイル詳細()
            fd.ファイル名 = filename
            fd.区分NO = MOVIE_START_J.区分番号
            fd.区分名 = MOVIE_START_J.区分名
            fd.ラウンドNO = MOVIE_START_J.ラウンド番号
            fd.ラウンド名 = MOVIE_START_J.ラウンド名
            fd.種目記号 = MOVIE_START_J.種目記号
            fd.種目名 = MOVIE_START_J.種目名
            fd.ヒート番号 = CInt(MOVIE_START_J.ヒート番号)
            fd.選手名 = Trim(MOVIE_START_J.リーダー名)
            fd.連番 = CInt(連番)
            fi.登録(fd)

            Dispatcher.Invoke(Sub() 録画スタート(folderPath, filename))
        End If
    End Sub

    Private Sub EVENT_RCV_KANS_MOVIE_STOP(ByVal sender As Object, ByVal e As ReceivedDataEventArgs) Handles _通信Main.RCV_KANS_MOVIE_STOP
        Dispatcher.Invoke(Sub()
                              Me.LB_Hedder.Content = ""
                              録画終了()
                          End Sub)
    End Sub

    '======================================================
    '   ブックマーク処理関連
    '======================================================

    Public Sub EVENT_RCV_ANSBMLIST(ByVal sender As Object, ByVal e As ReceivedDataEventArgs) Handles _通信Main.RCV_ANSBMLIST
        Dispatcher.Invoke(Sub() BM設定(e.ReceivedString))
    End Sub

    Private Sub BM設定(str As String)
        Dim ANSBMLIST As New ANSBMLIST_C(str)
        ReDim _ジャッジ記号(20)
        Dim ジャッジ数 As Integer = 0
        BMボタンクリア()

        For b = 1 To ANSBMLIST.BM数
            Dim FIND_FLAG As Integer = 0
            For j = 1 To ジャッジ数
                If _ジャッジ記号(j) = ANSBMLIST.ジャッジ記号(b) Then FIND_FLAG = j : j = ジャッジ数
            Next j

            If ANSBMLIST.ジャッジ記号(b) <> "START" AndAlso ANSBMLIST.ジャッジ記号(b) <> "END" Then
                If FIND_FLAG = 0 Then
                    ジャッジ数 += 1
                    _ジャッジ記号(ジャッジ数) = ANSBMLIST.ジャッジ記号(b)
                End If
                Dim btn As New Button()
                btn.Tag = ANSBMLIST.ジャッジ記号(b)
                btn.Background = System.Windows.Media.Brushes.Red
                btn.Height = 15
                btn.Width = 5
                btn.HorizontalAlignment = HorizontalAlignment.Left
                btn.Margin = New Thickness(ANSBMLIST.Get_横位置(b) * Canvas_BM.ActualWidth, 0, 0, 0)
                Canvas_BM.Children.Add(btn)
            End If
        Next b
    End Sub

    Private Sub BMボタンクリア()
        While Canvas_BM.Children.Count > 0
            Canvas_BM.Children.Remove(Canvas_BM.Children(0))
        End While
    End Sub

    '======================================================
    '   ユーティリティ
    '======================================================

    Private Function ファイル名安全化(元 As String) As String
        If String.IsNullOrEmpty(元) Then Return ""
        Dim s = 元
        s = s.Replace("/", "-").Replace("\", "-").Replace(":", "-")
        s = s.Replace("*", "").Replace("?", "").Replace("""", "'")
        s = s.Replace("<", "(").Replace(">", ")").Replace("|", "-")
        Return s
    End Function

    Private Function Get_ファイル名連番(path As String, filename As String) As String
        Dim rc As String = "01"
        Dim flist() As String
        Try
            flist = Directory.GetFiles(path, filename & "*.mp4", SearchOption.TopDirectoryOnly)
        Catch
            ReDim flist(1)
        End Try

        Dim maxVal As Integer = 0
        For Each a As String In flist
            If a <> "" Then
                Dim aList() As String = a.Split("_")
                If UBound(aList) > 1 Then
                    Dim 連番 = aList(UBound(aList)).Split(".")(0)
                    If IsNumeric(連番) AndAlso maxVal <= CInt(連番) Then maxVal = CInt(連番)
                End If
            End If
        Next

        If maxVal <= 98 Then rc = (maxVal + 1).ToString("00")
        Return rc
    End Function

    Protected Overrides Sub OnClosed(e As EventArgs)
        MyBase.OnClosed(e)
        ' カメラ停止
        If _camera IsNot Nothing Then
            _camera.Dispose()
            _camera = Nothing
        End If
        ' 音声停止
        If _waveSource IsNot Nothing Then
            _waveSource.Dispose()
        End If
        If _waveFile IsNot Nothing Then
            _waveFile.Dispose()
        End If
    End Sub

End Class
