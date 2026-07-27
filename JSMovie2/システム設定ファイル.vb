Imports System.IO

Public Class システム設定ファイル

    Public GM_IPAddress As String
    Public GM_Port As String
    Public 端末名 As String
    Public カメラ番号 As Integer
    Public VideoPath As String

    Private Const File頭文字列 As String = "Z_System"

    Sub New()
        GM_IPAddress = "127.0.0.1"
        GM_Port = "8080"
        端末名 = "端末1"
        カメラ番号 = 0
        VideoPath = ".\Data"
        FileRead()
    End Sub

    Private Sub FileRead()
        Dim filepath As String = AppDomain.CurrentDomain.BaseDirectory
        Dim filename As String = File頭文字列 & ".csv"
        Dim fullpath As String = Path.Combine(filepath, filename)

        If Not File.Exists(fullpath) Then Exit Sub

        Using stream As New FileStream(fullpath, FileMode.Open, FileAccess.Read)
            Dim cReader As New StreamReader(stream, System.Text.Encoding.UTF8)
            Dim stResult() As String = Split(cReader.ReadToEnd(), vbCrLf)
            Dim 行数 As Integer = stResult.Length - 1
            cReader.Close()

            Dim i, j As Integer
            For i = 0 To 行数
                Select Case stResult(i)
                    Case "[GM_IPAddress]"
                        For j = i + 1 To 行数
                            If Left(stResult(j), 2) <> "//" AndAlso Left(stResult(j), 1) <> "[" AndAlso stResult(j) <> "" Then
                                Me.GM_IPAddress = stResult(j) : j = 行数
                            ElseIf Left(stResult(j), 1) = "[" Then
                                j = 行数
                            End If
                        Next j
                    Case "[GM_Port]"
                        For j = i + 1 To 行数
                            If Left(stResult(j), 2) <> "//" AndAlso Left(stResult(j), 1) <> "[" AndAlso stResult(j) <> "" Then
                                Me.GM_Port = stResult(j) : j = 行数
                            ElseIf Left(stResult(j), 1) = "[" Then
                                j = 行数
                            End If
                        Next j
                    Case "[端末名]"
                        For j = i + 1 To 行数
                            If Left(stResult(j), 2) <> "//" AndAlso Left(stResult(j), 1) <> "[" AndAlso stResult(j) <> "" Then
                                Me.端末名 = stResult(j) : j = 行数
                            ElseIf Left(stResult(j), 1) = "[" Then
                                j = 行数
                            End If
                        Next j
                    Case "[カメラ番号]"
                        For j = i + 1 To 行数
                            If Left(stResult(j), 2) <> "//" AndAlso Left(stResult(j), 1) <> "[" AndAlso stResult(j) <> "" Then
                                Me.カメラ番号 = CInt(stResult(j)) : j = 行数
                            ElseIf Left(stResult(j), 1) = "[" Then
                                j = 行数
                            End If
                        Next j
                    Case "[VideoPath]"
                        For j = i + 1 To 行数
                            If Left(stResult(j), 2) <> "//" AndAlso Left(stResult(j), 1) <> "[" AndAlso stResult(j) <> "" Then
                                Me.VideoPath = stResult(j) : j = 行数
                            ElseIf Left(stResult(j), 1) = "[" Then
                                j = 行数
                            End If
                        Next j
                End Select
            Next i
        End Using
    End Sub

End Class
