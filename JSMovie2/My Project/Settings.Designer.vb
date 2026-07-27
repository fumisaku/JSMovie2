Option Strict On
Option Explicit On

Namespace My

    <System.Runtime.CompilerServices.CompilerGeneratedAttribute()>
    <System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "14.0.0.0")>
    Friend NotInheritable Partial Class MySettings
        Inherits System.Configuration.ApplicationSettingsBase

        Private Shared defaultInstance As MySettings = CType(System.Configuration.ApplicationSettingsBase.Synchronized(New MySettings()), MySettings)

        Public Shared ReadOnly Property [Default]() As MySettings
            Get
                Return defaultInstance
            End Get
        End Property

    End Class

End Namespace
