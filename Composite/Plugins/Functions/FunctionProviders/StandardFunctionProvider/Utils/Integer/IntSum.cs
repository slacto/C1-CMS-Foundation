﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Composite.Functions;
using Composite.C1Console.Security;
using Composite.Plugins.Functions.FunctionProviders.StandardFunctionProvider.Foundation;


namespace Composite.Plugins.Functions.FunctionProviders.StandardFunctionProvider.Utils.Integer
{
    internal sealed class Sum : StandardFunctionBase
	{
        public Sum(EntityTokenFactory entityTokenFactory)
            : base("Sum", "Composite.Utils.Integer", typeof(int), entityTokenFactory)
        {
        }


        public override object Execute(ParameterList parameters, FunctionContextContainer context)
        {
            IEnumerable<int> ints = parameters.GetParameter<IEnumerable<int>>("Ints");

            return ints.Sum();
        }

        protected override IEnumerable<StandardFunctionParameterProfile> StandardFunctionParameterProfiles
        {
            get 
            {
                yield return new StandardFunctionParameterProfile(
                    "Ints", 
                    typeof(IEnumerable<int>), 
                    true, 
                    new NoValueValueProvider(),
                    null);
            }
        }
	}
}
