Option Strict On
Option Explicit On

Namespace My.Resources

    <System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")>
    <System.Diagnostics.DebuggerNonUserCodeAttribute()>
    <System.Runtime.CompilerServices.CompilerGeneratedAttribute()>
    Friend NotInheritable Class Resources

        Private Shared resourceMan As System.Resources.ResourceManager
        Private Shared resourceCulture As System.Globalization.CultureInfo

        <System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")>
        Friend Sub New()
        End Sub

        <System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)>
        Friend Shared ReadOnly Property ResourceManager() As System.Resources.ResourceManager
            Get
                If Object.ReferenceEquals(resourceMan, Nothing) Then
                    Dim temp As System.Resources.ResourceManager = New System.Resources.ResourceManager("JSMovie2.Resources", GetType(Resources).Assembly)
                    resourceMan = temp
                End If
                Return resourceMan
            End Get
        End Property

        <System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)>
        Friend Shared Property Culture() As System.Globalization.CultureInfo
            Get
                Return resourceCulture
            End Get
            Set(ByVal value As System.Globalization.CultureInfo)
                resourceCulture = value
            End Set
        End Property

    End Class

End Namespace
