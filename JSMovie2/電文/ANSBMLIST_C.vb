Public Class ANSBMLIST_C

    Public 開始時刻 As DateTime
    Public 終了時刻 As DateTime
    Public 曲長さ As TimeSpan

    Public 区分番号 As String
    Public ラウンド番号 As String
    Public 種目記号 As String
    Public ヒート番号 As Integer
    Public BM数 As Integer

    Public ジャッジ記号() As String
    Public ジャッジ名() As String
    Public BM番号() As Integer
    Public 時刻() As String
    Public 経過時間() As TimeSpan
    Public タイマーカテゴリ() As String
    Public タイマー時間() As String

    Public Sub New(str As String)
        Dim parts() As String = str.Split(",")

        区分番号 = parts(5)
        ラウンド番号 = parts(6)
        種目記号 = parts(7)
        ヒート番号 = CInt(parts(8))
        BM数 = CInt(parts(9))

        ReDim ジャッジ記号(BM数)
        ReDim ジャッジ名(BM数)
        ReDim BM番号(BM数)
        ReDim 時刻(BM数)
        ReDim タイマーカテゴリ(BM数)
        ReDim タイマー時間(BM数)
        ReDim 経過時間(BM数)

        Dim h As Integer = 9
        For b = 1 To BM数
            h += 1 : ジャッジ記号(b) = parts(h)
            h += 1 : ジャッジ名(b) = parts(h)
            h += 1 : BM番号(b) = CInt(parts(h))
            h += 1 : 時刻(b) = parts(h)
            h += 1 : タイマーカテゴリ(b) = parts(h)
            h += 1 : タイマー時間(b) = parts(h)

            If ジャッジ記号(b) = "START" Then
                Dim dt As DateTime
                If DateTime.TryParse(時刻(b), dt) Then 開始時刻 = dt
            End If
            If ジャッジ記号(b) = "END" Then
                Dim dt As DateTime
                If DateTime.TryParse(時刻(b), dt) Then 終了時刻 = dt
            End If
        Next b

        曲長さ = New TimeSpan(終了時刻.Subtract(開始時刻).Ticks)
    End Sub

    Public Function Get_横位置(bmNo As Integer) As Double
        Dim format As String = "yyyy/MM/dd HH:mm:ss"
        Dim 開始時刻日付 As String = 開始時刻.ToShortDateString()
        Dim bmDt As DateTime = DateTime.ParseExact(開始時刻日付 & " " & 時刻(bmNo), format, Nothing)
        経過時間(bmNo) = New TimeSpan(bmDt.Subtract(開始時刻).Ticks)

        Dim rc As Double = If(曲長さ.TotalSeconds > 0, 経過時間(bmNo).TotalSeconds / 曲長さ.TotalSeconds, 0)
        Return Math.Max(0, Math.Min(1, rc))
    End Function

End Class
