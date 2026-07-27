Public Class ジャッジリスト_C

    Public ジャッジ数 As Integer
    Public ジャッジ記号() As String
    Public ジャッジ名() As String

    Sub New(count As Integer)
        ジャッジ数 = count
        ReDim ジャッジ記号(count)
        ReDim ジャッジ名(count)
    End Sub

End Class
