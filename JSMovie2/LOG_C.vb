Imports System
Imports System.IO
Imports System.Text

Public Class LOG_C

    Private ON_OFF_Flag As String
    Private LOG_Filename As String
    Private LOG_Level As Integer

    Public ReadOnly ERR As Integer = 1
    Public ReadOnly WARNING As Integer = 2
    Public ReadOnly INFO As Integer = 3
    Public ReadOnly DEBUG As Integer = 4
    Public ReadOnly DEB_Detail As Integer = 5

    Public Sub New()
        LOG_Level = 1
        ON_OFF_Flag = "OFF"
    End Sub

    Public Sub SetLogLevel(Level As Integer)
        LOG_Level = Level
    End Sub

    Public Function CreateFile() As String
        ON_OFF_Flag = "ON"
        Dim logPath As String = AppDomain.CurrentDomain.BaseDirectory
        LOG_Filename = Path.Combine(logPath, "LOG" & Format(Now, "yyyyMMddHHmmss") & ".log")
        Return LOG_Filename
    End Function

    Public Sub LogAdd(ByVal cmt As String, ByVal Level As Integer)
        If ON_OFF_Flag = "ON" AndAlso Level <= LOG_Level Then
            Try
                Using writer = New StreamWriter(LOG_Filename, True, Encoding.UTF8)
                    writer.WriteLine(Format(Now, "yyyy/MM/dd") & " " & Format(Now, "HH:mm:ss") & " " & Level & " " & cmt)
                End Using
            Catch
            End Try
        End If
    End Sub

    Public Sub Set_ON()
        ON_OFF_Flag = "ON"
    End Sub

    Public Sub Set_OFF()
        ON_OFF_Flag = "OFF"
    End Sub

End Class
