Imports System
Imports System.Net
Imports System.Net.Sockets

Public Delegate Sub ReceivedDataEventHandler(ByVal sender As Object, ByVal e As ReceivedDataEventArgs)

Public Class ReceivedDataEventArgs
    Inherits EventArgs

    Public ReadOnly Property ReceivedString As String
    Public ReadOnly Property Client As Socket

    Public Sub New(client As Socket, str As String)
        Me.Client = client
        Me.ReceivedString = str
    End Sub
End Class

Public Class TCPClient

    Private _socket As Socket
    Private _maxReceiveLength As Integer
    Private _encoding As System.Text.Encoding
    Private Parm As Parm_C
    Protected receivedBytes As System.IO.MemoryStream
    Private _localEndPoint As IPEndPoint
    Private _remoteEndPoint As IPEndPoint

    Public Sub New(Parm_ As Parm_C)
        Parm = Parm_
        Parm.LOG.LogAdd("Start Connect to Server", Parm.LOG.DEBUG)
        _socket = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        _maxReceiveLength = 1024000
        _encoding = System.Text.Encoding.UTF8
    End Sub

    Public Sub Connect(ByVal host As String, ByVal port As Integer)
        If Me.IsClosed Then
            Parm.LOG.LogAdd("Connect失敗。閉じています。", Parm.LOG.ERR)
            Exit Sub
        End If

        Dim ipEnd As IPEndPoint
        If System.Net.IPAddress.TryParse(host, Nothing) Then
            Dim ipAdrs As IPAddress = IPAddress.Parse(host)
            ipEnd = New IPEndPoint(ipAdrs, port)
        Else
            ipEnd = New IPEndPoint(Dns.GetHostEntry(host).AddressList(0), port)
        End If

        Me._socket.Connect(ipEnd)
        Me._localEndPoint = CType(Me._socket.LocalEndPoint, IPEndPoint)
        Me._remoteEndPoint = CType(Me._socket.RemoteEndPoint, IPEndPoint)
        Parm.LOG.LogAdd("Connect 成功。サーバ：" & ipEnd.ToString(), Parm.LOG.INFO)
        Me.StartReceive()
    End Sub

    Public Sub StartReceive()
        If Me.IsClosed Then
            Parm.LOG.LogAdd("StartReceive失敗。閉じています。", Parm.LOG.ERR)
            Exit Sub
        End If
        Dim receiveBuffer(1023) As Byte
        Me.receivedBytes = New System.IO.MemoryStream()
        Me._socket.BeginReceive(receiveBuffer, 0, receiveBuffer.Length,
            SocketFlags.None,
            New AsyncCallback(AddressOf ReceiveDataCallback), receiveBuffer)
        Parm.LOG.LogAdd("StartReceive開始", Parm.LOG.INFO)
    End Sub

    Private Sub ReceiveDataCallback(ByVal ar As IAsyncResult)
        Dim len As Integer = -1
        Try
            SyncLock Me
                len = Me._socket.EndReceive(ar)
            End SyncLock
        Catch
        End Try

        If len <= 0 Then
            Me.Close()
            Return
        End If

        Dim receiveBuffer As Byte() = CType(ar.AsyncState, Byte())
        Me.receivedBytes.Write(receiveBuffer, 0, len)

        If Me.receivedBytes.Length > _maxReceiveLength Then
            ' 最大受信長を超えた場合はバッファを捨てて受信を継続する（接続は維持）
            ' KANS_MU_Progress 等の大きな電文を受け取っても通信が切れないようにする
            Parm.LOG.LogAdd("最大受信長を超えたためバッファをリセットします。受信長=" & Me.receivedBytes.Length, Parm.LOG.WARNING)
            Me.receivedBytes.Close()
            Me.receivedBytes = New System.IO.MemoryStream()
            ' 次の受信を継続
            SyncLock Me
                Me._socket.BeginReceive(receiveBuffer, 0, receiveBuffer.Length,
                    SocketFlags.None,
                    New AsyncCallback(AddressOf ReceiveDataCallback), receiveBuffer)
            End SyncLock
            Return
        End If

        If Me.receivedBytes.Length >= 2 Then
            Me.receivedBytes.Seek(-2, System.IO.SeekOrigin.End)
            If Me.receivedBytes.ReadByte() = 13 AndAlso Me.receivedBytes.ReadByte() = 10 Then
                Dim str As String = _encoding.GetString(Me.receivedBytes.ToArray())
                Me.receivedBytes.Close()
                Dim startPos As Integer = 0
                Dim endPos As Integer
                While True
                    endPos = str.IndexOf(vbCrLf, startPos)
                    If endPos < 0 Then Exit While
                    Dim line As String = str.Substring(startPos, endPos - startPos)
                    startPos = endPos + 2
                    Me.OnReceivedData(New ReceivedDataEventArgs(Nothing, line))
                    Parm.LOG.LogAdd("受信データ:" & line, Parm.LOG.DEB_Detail)
                End While
                Me.receivedBytes = New System.IO.MemoryStream()
            Else
                Me.receivedBytes.Seek(0, System.IO.SeekOrigin.End)
            End If
        End If

        SyncLock Me
            Me._socket.BeginReceive(receiveBuffer, 0, receiveBuffer.Length,
                SocketFlags.None,
                New AsyncCallback(AddressOf ReceiveDataCallback), receiveBuffer)
        End SyncLock
    End Sub

    Public Sub Send(ByVal str As String)
        If Me.IsClosed Then
            Parm.LOG.LogAdd("Send失敗。閉じています。", Parm.LOG.ERR)
            Exit Sub
        End If
        Dim sendBytes As Byte() = _encoding.GetBytes(str & vbCrLf)
        Me._socket.Send(sendBytes)
        Parm.LOG.LogAdd("データ送信:" & str, Parm.LOG.DEB_Detail)
    End Sub

    Public Sub Close()
        SyncLock Me
            If Me.IsClosed Then Return
            Me._socket.Shutdown(SocketShutdown.Both)
            Me._socket.Close()
            Me._socket = Nothing
            If Me.receivedBytes IsNot Nothing Then
                Me.receivedBytes.Close()
                Me.receivedBytes = Nothing
            End If
        End SyncLock
        Me.OnDisconnected(New EventArgs())
        Parm.LOG.LogAdd("サーバーとの接続を切断しました。", Parm.LOG.INFO)
    End Sub

    Public ReadOnly Property IsClosed() As Boolean
        Get
            Return Me._socket Is Nothing
        End Get
    End Property

    Public Event ReceivedData As ReceivedDataEventHandler
    Protected Overridable Sub OnReceivedData(ByVal e As ReceivedDataEventArgs)
        RaiseEvent ReceivedData(Me, e)
    End Sub

    Public Event Connected As EventHandler
    Protected Overridable Sub OnConnected(ByVal e As EventArgs)
        RaiseEvent Connected(Me, e)
    End Sub

    Public Event Disconnected As EventHandler
    Protected Overridable Sub OnDisconnected(ByVal e As EventArgs)
        RaiseEvent Disconnected(Me, e)
    End Sub

End Class
