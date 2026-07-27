Public Class KANS_MB_KUBUN

    Public 区分番号() As String
    Public 区分名() As String
    Public ラウンド番号() As String
    Public ラウンド名() As String
    Public レコード数 As Integer

    Sub New()
        ReDim 区分番号(100)
        ReDim 区分名(100)
        ReDim ラウンド番号(100)
        ReDim ラウンド名(100)
        レコード数 = 0
    End Sub

    Public Sub Denbun_Set(str As String)
        Dim parts() As String = str.Split(",")
        Dim no As Integer = CInt(parts(4))
        If no = 1 Then レコード数 = 0

        レコード数 = Math.Max(レコード数, no)
        If no <= 100 Then
            区分番号(no) = If(UBound(parts) >= 5, parts(5), "")
            区分名(no) = If(UBound(parts) >= 6, parts(6), "")
            ラウンド番号(no) = If(UBound(parts) >= 7, parts(7), "")
            ラウンド名(no) = If(UBound(parts) >= 8, parts(8), "")
        End If
    End Sub

End Class
