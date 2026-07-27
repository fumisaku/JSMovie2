Public Class ヒート情報_C

    Public 背番号() As String
    Public PCS番号() As String
    Public 点数() As String
    Public 減点詳細(,) As String

    Sub New(maxCount As Integer)
        ReDim 背番号(maxCount * 2 + 1)
        ReDim PCS番号(maxCount)
        ReDim 点数(maxCount * 2 + 1)
        ReDim 減点詳細(maxCount, 20)
    End Sub

End Class
