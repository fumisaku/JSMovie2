'index.csv 用Class

Public Class F_Index

    Public リスト(100) As FD_ファイル詳細
    Public 登録済みレコード数 As Integer
    Private ReadOnly filepath As String
    Private Filename As String = "index.csv"

    Sub New(filepath_ As String)
        登録済みレコード数 = 0
        filepath = filepath_
        FileRead()
    End Sub

    Public Function 登録(データ As FD_ファイル詳細) As Integer
        Dim rc As Integer = 0
        Try
            Dim sw As New System.IO.StreamWriter(System.IO.Path.Combine(filepath, Filename), False, System.Text.Encoding.UTF8)
            sw.WriteLine("FileName,区分No,区分名,ラウンドNo,ラウンド名,種目No,種目名,Heat,選手名,連番")

            Dim 登録済みFLAG As Boolean = False
            For s = 1 To 登録済みレコード数
                Dim 元記号 = リスト(s).ファイル名
                Dim 新記号 = データ.ファイル名
                If 元記号 = 新記号 Then
                    sw.WriteLine(カンマ区切り(データ))
                    登録済みFLAG = True
                Else
                    sw.WriteLine(カンマ区切り(リスト(s)))
                End If
            Next s

            If Not 登録済みFLAG Then
                sw.WriteLine(カンマ区切り(データ))
            End If
            sw.Close()
        Catch ex As Exception
            rc = 1
        End Try
        FileRead()
        Return rc
    End Function

    Public Sub FileRead()
        ReDim リスト(100)
        登録済みレコード数 = 0

        Dim fullpath As String = System.IO.Path.Combine(filepath, Filename)
        If Not System.IO.File.Exists(fullpath) Then Exit Sub

        ' detectEncodingFromByteOrderMarks=True でBOMがあればUTF-8、なければShift-JISにフォールバック
        Dim cReader As New System.IO.StreamReader(fullpath, System.Text.Encoding.GetEncoding("shift_jis"), True)
        Dim lines As New System.Collections.Generic.List(Of String)
        While cReader.Peek() >= 0
            lines.Add(cReader.ReadLine())
        End While
        cReader.Close()

        ' UTF-8で書き直す（次回以降はUTF-8で読める）
        Try
            Dim sw As New System.IO.StreamWriter(fullpath, False, System.Text.Encoding.UTF8)
            For Each line In lines
                sw.WriteLine(line)
            Next
            sw.Close()
        Catch
        End Try

        For Each stBuffer In lines
            If Not stBuffer.StartsWith("FileName") Then
                Addデータ(stBuffer)
            End If
        Next
    End Sub

    Private Function カンマ区切り(fd As FD_ファイル詳細) As String
        Return fd.ファイル名 & "," & fd.区分NO & "," & fd.区分名 & "," & fd.ラウンドNO & "," & fd.ラウンド名 & "," &
               fd.種目記号 & "," & fd.種目名 & "," & fd.ヒート番号.ToString() & "," & fd.選手名 & "," & fd.連番.ToString()
    End Function

    Private Sub Addデータ(データ As String)
        Dim arBuffer() As String = データ.Split(",")
        Dim No As Integer = 登録済みレコード数 + 1

        If UBound(arBuffer) >= 6 Then
            リスト(No) = New FD_ファイル詳細
            リスト(No).ファイル名 = arBuffer(0)
            リスト(No).区分NO = arBuffer(1)
            リスト(No).区分名 = arBuffer(2)
            リスト(No).ラウンドNO = arBuffer(3)
            リスト(No).ラウンド名 = arBuffer(4)
            リスト(No).種目記号 = arBuffer(5)
            リスト(No).種目名 = arBuffer(6)
            If UBound(arBuffer) >= 7 Then Integer.TryParse(arBuffer(7), リスト(No).ヒート番号)
            If UBound(arBuffer) >= 8 Then リスト(No).選手名 = arBuffer(8)
            If UBound(arBuffer) >= 9 Then Integer.TryParse(arBuffer(9), リスト(No).連番)
        End If
        登録済みレコード数 += 1
    End Sub

    Public Function Get_FDファイル詳細(ファイル名 As String) As FD_ファイル詳細
        Dim rc As FD_ファイル詳細 = Nothing
        For i = 1 To 登録済みレコード数
            If リスト(i).ファイル名 = ファイル名 Then
                rc = リスト(i)
                i = 登録済みレコード数
            End If
        Next i
        Return rc
    End Function

    Public Class FD_ファイル詳細
        Public ファイル名 As String
        Public 区分NO As String
        Public 区分名 As String
        Public ラウンドNO As String
        Public ラウンド名 As String
        Public 種目記号 As String
        Public 種目名 As String
        Public ヒート番号 As Integer
        Public 選手名 As String
        Public 連番 As Integer

        Public Sub 登録(データ As FD_ファイル詳細)
            ファイル名 = データ.ファイル名
            区分NO = データ.区分NO
            区分名 = データ.区分名
            ラウンドNO = データ.ラウンドNO
            ラウンド名 = データ.ラウンド名
            種目記号 = データ.種目記号
            種目名 = データ.種目名
            ヒート番号 = データ.ヒート番号
            選手名 = データ.選手名
            連番 = データ.連番
        End Sub
    End Class

End Class
