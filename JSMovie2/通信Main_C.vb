Imports System.Threading
Imports System.Windows
Imports System.Windows.Threading

Public Class 通信Main_C

    Private システム設定 As システム設定ファイル
    Private Parm As Parm_C
    Private LOG As LOG_C
    Private Denbun As Denbun_C
    Private WithEvents TCPClientInst As TCPClient
    Private 端末名 As String = "JSP01"
    Private TimeoutFlag As Boolean
    Private Timer_sendTimeout As DispatcherTimer

    Sub main()
        Parm = New Parm_C()
        LOG = New LOG_C()
        LOG.SetLogLevel(5)
        LOG.Set_ON()
        LOG.CreateFile()
        Parm.LOG = LOG

        システム設定 = New システム設定ファイル()
        端末名 = システム設定.端末名

        Timer_sendTimeout = New DispatcherTimer()
        Timer_sendTimeout.IsEnabled = False
        AddHandler Timer_sendTimeout.Tick, New EventHandler(AddressOf Timer_sendTimeout_Tick)

        Dim rc As Integer = ServerConnect()
        If rc = 0 Then
            RaiseEvent SVR_Connected(Me, New EventArgs())
            Send_KREQ_MA_COMP()
        Else
            RaiseEvent SVR_DisConnected(Me, New EventArgs())
        End If
    End Sub

    Private Function ServerConnect() As Integer
        TCPClientInst = New TCPClient(Parm)
        Try
            TCPClientInst.Connect(システム設定.GM_IPAddress, CInt(システム設定.GM_Port))
        Catch ex As Exception
            Parm.LOG.LogAdd("Connectに失敗しました。" & ex.Message, Parm.LOG.ERR)
            TCPClientInst = Nothing
            Return 1
        End Try
        Return 0
    End Function

    Private Sub Send_KREQ_MA_COMP()
        Timer_sendTimeout.Interval = New TimeSpan(0, 0, 10)
        TimeoutFlag = False
        Timer_sendTimeout.Start()

        If TCPClientInst.IsClosed Then Call ServerConnect()
        TCPClientInst.Send("JS,KREQ_MA_COMP," & 端末名 & ",1,1")
    End Sub

    Public Sub Send_KREQ_MB_KUBUN()
        Timer_sendTimeout.Interval = New TimeSpan(0, 0, 10)
        TimeoutFlag = False
        Timer_sendTimeout.Start()
        If TCPClientInst.IsClosed Then Call ServerConnect()
        TCPClientInst.Send("JS,KREQ_MB_KUBUN," & 端末名 & ",1,1")
    End Sub

    Public Sub Send_REQ_BMLIST(str As String)
        If TCPClientInst.IsClosed Then Call ServerConnect()
        TCPClientInst.Send(str)
    End Sub

    Private Sub Timer_sendTimeout_Tick(sender As Object, e As EventArgs)
        TimeoutFlag = True
        Timer_sendTimeout.Stop()
        System.Windows.MessageBox.Show("タイムアウトしました", "タイムアウト", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning)
    End Sub

    Private Sub server_ReceivedData(ByVal sender As Object, ByVal e As ReceivedDataEventArgs) Handles TCPClientInst.ReceivedData
        Dim str As String = e.ReceivedString
        Dim parts() As String = str.Split(",")
        If UBound(parts) < 4 Then Exit Sub

        If CInt(parts(4)) = 1 Then
            Denbun = New Denbun_C()
        End If
        Denbun.AddMsg(str)

        If parts(3) = parts(4) Then
            TimeoutFlag = True
            Timer_sendTimeout.Stop()

            Select Case parts(1)
                Case "KANS_MA_COMP"
                    RaiseEvent RCV_KANS_MA_COMP(Me, e)
                Case "KANS_MB_KUBUN"
                    RaiseEvent RCV_KANS_MB_KUBUN(Me, e)
                Case "KANS_MU_Progress"
                    RaiseEvent RCV_KANS_MU_Progress(Me, e)
                Case "KANS_MOVIE_START"
                    RaiseEvent RCV_KANS_MOVIE_START(Me, e)
                Case "KANS_MOVIE_STOP"
                    RaiseEvent RCV_KANS_MOVIE_STOP(Me, e)
                Case "ANSBMLIST"
                    RaiseEvent RCV_ANSBMLIST(Me, e)
            End Select
        End If
    End Sub

    Public Function IsClosed() As Boolean
        If TCPClientInst Is Nothing Then Return True
        Return TCPClientInst.IsClosed
    End Function

    Public Event SVR_Connected(ByVal sender As Object, ByVal e As EventArgs)
    Public Event SVR_DisConnected(ByVal sender As Object, ByVal e As EventArgs)
    Public Event RCV_KANS_MA_COMP(ByVal sender As Object, ByVal e As ReceivedDataEventArgs)
    Public Event RCV_KANS_MB_KUBUN(ByVal sender As Object, ByVal e As ReceivedDataEventArgs)
    Public Event RCV_KANS_MU_Progress(ByVal sender As Object, ByVal e As ReceivedDataEventArgs)
    Public Event RCV_KANS_MOVIE_START(ByVal sender As Object, ByVal e As ReceivedDataEventArgs)
    Public Event RCV_KANS_MOVIE_STOP(ByVal sender As Object, ByVal e As ReceivedDataEventArgs)
    Public Event RCV_ANSBMLIST(ByVal sender As Object, ByVal e As ReceivedDataEventArgs)

End Class
