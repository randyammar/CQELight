﻿using Autofac;
using CQELight.TestFramework;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace CQELight.IoC.Autofac.Integration.Tests
{
    public class AutofacScopeTests : BaseUnitTestClass
    {

        #region Ctor & members

        private interface IScopeTest { string Data { get; } }
        private class ScopeTest : IScopeTest
        {
            public string Data { get; }

            public ScopeTest()
            {
                Data = "ctor";
            }
            public ScopeTest(string data)
            {
                Data = data;
            }
        }

        private interface IParameterResolving { string Data { get; } }
        private class ParameterResolving : IParameterResolving
        {
            public ParameterResolving(string data)
            {
                Data = data;
            }

            public string Data { get; }
        }

        public AutofacScopeTests()
        {

        }

        #endregion

        #region CreateChildScope

        [Fact]
        public void AutofacScope_CreateChildScope_CustomScopeRegistration_TypeRegistration_AsExpected()
        {
            new Bootstrapper().UseAutofacAsIoC(new ContainerBuilder());

            using (var s = DIManager.BeginScope())
            {
                var i = s.Resolve<IScopeTest>();
                i.Should().BeNull();
                using (var sChild = s.CreateChildScope())
                {
                    i = sChild.Resolve<IScopeTest>();
                    i.Should().BeNull();
                }
                using (var sChild = s.CreateChildScope(e => e.RegisterType<ScopeTest>()))
                {
                    i = sChild.Resolve<IScopeTest>();
                    i.Should().NotBeNull();
                    i.Data.Should().Be("ctor");
                }
            }
        }

        [Fact]
        public void AutofacScope_CreateChildScope_CustomScopeRegistration_InstanceRegistration_AsExpected()
        {
            new Bootstrapper().UseAutofacAsIoC(new ContainerBuilder());

            using (var s = DIManager.BeginScope())
            {
                var i = s.Resolve<IScopeTest>();
                i.Should().BeNull();
                using (var sChild = s.CreateChildScope())
                {
                    i = sChild.Resolve<IScopeTest>();
                    i.Should().BeNull();
                }
                using (var sChild = s.CreateChildScope(e => e.Register(new ScopeTest("instance"))))
                {
                    i = sChild.Resolve<IScopeTest>();
                    i.Should().NotBeNull();
                    i.Data.Should().Be("instance");
                }
            }
        }

        #endregion

        #region Parameters

        [Fact]
        public void AutofacScope_Resolve_TypeParameter()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<ParameterResolving>().AsImplementedInterfaces();
            new Bootstrapper().UseAutofacAsIoC(builder);

            using (var s = DIManager.BeginScope())
            {
                var i = s.Resolve<IParameterResolving>(new TypeResolverParameter(typeof(string), "test"));
                i.Data.Should().Be("test");
            }
        }


        [Fact]
        public void AutofacScope_Resolve_NameParameter()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<ParameterResolving>().AsImplementedInterfaces();
            new Bootstrapper().UseAutofacAsIoC(builder);

            using (var s = DIManager.BeginScope())
            {
                var i = s.Resolve<IParameterResolving>(new NameResolverParameter("data", "name_test"));
                i.Data.Should().Be("name_test");
            }
        }

        #endregion

    }
}
