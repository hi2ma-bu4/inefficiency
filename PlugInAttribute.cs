using System;

namespace PlugInAttribute
{
	//Assemblyがプラグインかどうかの判定をするための属性
	[System.AttributeUsage(System.AttributeTargets.Assembly)]
	public class PlugInAssemblyAttribute : Attribute
	{
		public PlugInAssemblyAttribute() : base()
		{
		}
	}
	//クラスがプラグインのクラスかどうかの判定をするための属性
	[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true )]
	public class PlugInClassAttribute : Attribute
	{
		public String ExecuteName { get; set; }
		public Int32 ClassIndex { get; set; }
		public Int32 MethodIndex { get; set; }
		public PlugInClassAttribute(
					string executeName,
					Int32 classIndex, Int32 methodIndex):base()
		{
			ExecuteName = executeName;
			ClassIndex = classIndex;
			MethodIndex = methodIndex;
		}
	}
}