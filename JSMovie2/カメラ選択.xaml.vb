Public Class カメラ選択

    Public ReadOnly Property SelectedCameraIndex As Integer = 0

    Private Sub PB_設定_Click(sender As Object, e As System.Windows.RoutedEventArgs)
        If RB_00.IsChecked = True Then
            _SelectedCameraIndex = 0
        ElseIf RB_01.IsChecked = True Then
            _SelectedCameraIndex = 1
        ElseIf RB_02.IsChecked = True Then
            _SelectedCameraIndex = 2
        End If
        Me.DialogResult = True
        Me.Close()
    End Sub

End Class
