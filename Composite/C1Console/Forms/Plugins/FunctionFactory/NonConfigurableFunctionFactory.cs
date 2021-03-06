using Microsoft.Practices.ObjectBuilder;

using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration.ObjectBuilder;
using System;


namespace Composite.C1Console.Forms.Plugins.FunctionFactory
{
    /// <summary>    
    /// </summary>
    /// <exclude />
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)] 
    [Assembler(typeof(NonConfigurableFunctionFactoryAssembler))]
    public sealed class NonConfigurableFunctionFactory : FunctionFactoryData
    {
    }



    internal sealed class NonConfigurableFunctionFactoryAssembler : IAssembler<IFormFunctionFactory, FunctionFactoryData>
    {
        public IFormFunctionFactory Assemble(IBuilderContext context, FunctionFactoryData objectConfiguration, IConfigurationSource configurationSource, ConfigurationReflectionCache reflectionCache)
        {
            return (IFormFunctionFactory)Activator.CreateInstance(objectConfiguration.Type);            
        }
    }
}
