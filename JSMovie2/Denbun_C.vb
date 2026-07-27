Public Class Denbun_C

    Public 競技会名 As String
    Public ANSKUBUN_区分番号() As String
    Public ANSKUBUN_区分名() As String
    Public ANSKUBUN_ラウンド番号() As String
    Public ANSKUBUN_ラウンド名() As String
    Public ANSKUBUN_ジャッジ数() As Integer
    Public ANSKUBUN_ジャッジリスト() As ジャッジリスト_C
    Public ANSKUBUN_ジャッジ記号(100) As String
    Public ANSKUBUN_ジャッジ名(100) As String
    Public ジャッジリスト As ジャッジリスト_C

    Public ANSHEAT_区分番号 As String
    Public ANSHEAT_区分名 As String
    Public ANSHEAT_ラウンド番号 As String
    Public ANSHEAT_ラウンド名 As String
    Public ANSHEAT_採点方式 As String
    Public ANSHEAT_ジャッジ記号 As String
    Public ANSHEAT_ジャッジ名 As String
    Public ANSHEAT_ジャッジ区分 As String
    Public ANSHEAT_種目記号 As String
    Public ANSHEAT_種目名 As String
    Public ANSHEAT_ソログループ区分 As String
    Public ANSHEAT_ヒート数 As Integer
    Public ANSHEAT_Cali_最大値 As String
    Public ANSHEAT_Cali_最小値 As String
    Public ANSHEAT_A_PCS名(10) As String
    Public ANSHEAT_タイマー設定(10) As String
    Public ANSHEAT_減点項目数 As Integer
    Public ANSHEAT_減点項目名(20) As String
    Public ANSHEAT_減点初期値(20) As String
    Public ANSHEAT_減点STEP値(20) As String
    Public ANSHEAT_減点MAX値(20) As String
    Public ANSHEAT_A_ヒート番号() As String
    Public ANSHEAT_A_ヒート毎出場選手数() As Integer
    Public ヒート情報 As ヒート情報_C
    Public KANS_MB_KUBUN As KANS_MB_KUBUN

    Public Sub AddMsg(msg As String)
        Dim 配列() As String = msg.Split(",")

        If 配列(0) = "JS" Then
            Select Case 配列(1)
                Case "ANSKUBUN"
                    ANSKUBUN(配列)
                Case "ANSHEAT"
                    ANSHEAT_Proc(配列)
            End Select
        ElseIf 配列(0) = "JK" Then
            Select Case 配列(1)
                Case "KANS_MB_KUBUN"
                    If CInt(配列(4)) = 1 Then
                        KANS_MB_KUBUN = New KANS_MB_KUBUN()
                    End If
                    KANS_MB_KUBUN.Denbun_Set(msg)
            End Select
        End If
    End Sub

    Private Sub ANSKUBUN(配列() As String)
        Dim レコード番号 As Integer = CInt(配列(4))
        Dim 全レコード数 As Integer = CInt(配列(3))

        If レコード番号 = 1 Then
            競技会名 = 配列(5)
            ReDim ANSKUBUN_区分番号(全レコード数)
            ReDim ANSKUBUN_区分名(全レコード数)
            ReDim ANSKUBUN_ラウンド番号(全レコード数)
            ReDim ANSKUBUN_ラウンド名(全レコード数)
            ReDim ANSKUBUN_ジャッジ数(全レコード数)
            ReDim ANSKUBUN_ジャッジリスト(全レコード数)
        End If

        ANSKUBUN_区分番号(レコード番号) = 配列(6)
        ANSKUBUN_区分名(レコード番号) = 配列(7)
        ANSKUBUN_ラウンド番号(レコード番号) = 配列(8)
        ANSKUBUN_ラウンド名(レコード番号) = 配列(9)
        ANSKUBUN_ジャッジ数(レコード番号) = CInt(配列(10))
        ジャッジリスト = New ジャッジリスト_C(CInt(配列(10)))
        ANSKUBUN_ジャッジリスト(レコード番号) = ジャッジリスト

        Dim j As Integer = 11
        Dim 空白行 As Integer = 1
        For i = 1 To ANSKUBUN_ジャッジ数(レコード番号)
            ジャッジリスト.ジャッジ記号(i) = 配列(j) : j += 1
            ジャッジリスト.ジャッジ名(i) = 配列(j) : j += 1

            Dim 発見FLAG As Boolean = False
            For k = 1 To 100
                If ANSKUBUN_ジャッジ記号(k) = ジャッジリスト.ジャッジ記号(i) Then
                    発見FLAG = True : k = 100
                ElseIf ANSKUBUN_ジャッジ記号(k) = "" Then
                    空白行 = k : k = 100
                End If
            Next k
            If Not 発見FLAG Then
                ANSKUBUN_ジャッジ記号(空白行) = ジャッジリスト.ジャッジ記号(i)
                ANSKUBUN_ジャッジ名(空白行) = ジャッジリスト.ジャッジ名(i)
            End If
        Next i
    End Sub

    Private Sub ANSHEAT_Proc(配列() As String)
        Dim レコード番号 As Integer = CInt(配列(4))

        ANSHEAT_区分番号 = 配列(5)
        ANSHEAT_区分名 = 配列(6)
        ANSHEAT_ラウンド番号 = 配列(7)
        ANSHEAT_ラウンド名 = 配列(8)
        ANSHEAT_採点方式 = 配列(9)
        ANSHEAT_ジャッジ記号 = 配列(10)
        ANSHEAT_ジャッジ名 = 配列(11)
        ANSHEAT_ジャッジ区分 = 配列(12)
        ANSHEAT_種目記号 = 配列(13)
        ANSHEAT_種目名 = 配列(14)
        ANSHEAT_ソログループ区分 = 配列(15)
        ANSHEAT_ヒート数 = CInt(配列(16))

        If レコード番号 = 1 Then
            ReDim ANSHEAT_A_ヒート番号(ANSHEAT_ヒート数)
            ReDim ANSHEAT_A_ヒート毎出場選手数(ANSHEAT_ヒート数)
        End If

        ANSHEAT_A_ヒート番号(レコード番号) = 配列(17)
        ANSHEAT_A_ヒート毎出場選手数(レコード番号) = CInt(配列(18))
    End Sub

End Class
