using System;
using NUnit.Framework;
using ProtoCore.DSASM.Mirror;
using ProtoCore.Lang;
namespace ProtoTest.MultiLangTests
{
    [TestFixture]
    public class AssociativeToImperative
    {
        public ProtoCore.Core core;
        [SetUp]
        public void Setup()
        {
            core = new ProtoCore.Core(new ProtoCore.Options());
            core.Executives.Add(ProtoCore.Language.kAssociative, new ProtoAssociative.Executive(core));
            core.Executives.Add(ProtoCore.Language.kImperative, new ProtoImperative.Executive(core));
        }

        [Test]
        public void EmbeddedTest001()
        {
            String code =
@"x;[Associative]
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Obj o = mirror.GetValue("x");
            Assert.IsTrue((Int64)o.Payload == 5);
        }

        [Test]
        public void EmbeddedTest002()
        {
            String code =
@"
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Obj o = mirror.GetValue("x");
            Assert.IsTrue((Int64)o.Payload == 5);
        }

        [Test]
        public void EmbeddedTest003()
        {
            String code =
@"
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Obj o = mirror.GetValue("x");
            Assert.IsTrue((Int64)o.Payload == 6);
        }

        [Test]
        [Category("Modifier Block")]
        public void EmbeddedTest004()
        {

            Assert.Fail("This code should fail as x@first should be read only, however it doesn't");
            String code =
@"x;
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Obj o = mirror.GetValue("x");
            Assert.IsTrue((Int64)o.Payload == 7); //0 => x@first, 1 x@second, 6 -> x@first 7 -> x@second
        }

        [Test]
        [Category("Modifier Block")]
        public void EmbeddedTest005()
        {
            Assert.Fail("This code should fail as x@second should be read only, however it doesn't");
            String code =
@"x;
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Obj o = mirror.GetValue("x");
            Assert.IsTrue((Int64)o.Payload == 6);
        }

        [Test]
        public void EmbeddedTest006()
        {
            String code =
                @"
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Assert.IsTrue((Int64)mirror.GetValue("x").Payload == 1);
        }

        [Test]
        public void ImperativeUpdate001()
        {
            String code =
@"
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Obj o = mirror.GetValue("b");
            Assert.IsTrue((Int64)o.Payload == 10);
        }

        [Test]
        public void ImperativeUpdate002()
        {
            String code =
@"
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Obj o = mirror.GetValue("a");
            Assert.IsTrue((Int64)o.Payload == 10);
        }

        [Test]
        public void LanguageBlockReturn01()
        {
            String code =
@"
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Assert.IsTrue((Int64)mirror.GetValue("a").Payload == 0);
        }

        [Test]
        public void LanguageBlockReturn02()
        {
            String code =
@"
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Assert.IsTrue((Int64)mirror.GetValue("a").Payload == 1020);
        }

        [Test]
        public void NestedBlockInFunction01()
        {
            String code =
@"
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Assert.IsTrue((Int64)mirror.GetValue("a").Payload == 48);
        }

        [Test]
        public void NestedBlockInFunction02()
        {
            String code =
@"
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Assert.IsTrue((Int64)mirror.GetValue("a").Payload == 100);
        }

        [Test]
        public void NestedBlockInFunction03()
        {
            String code =
@"
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Assert.IsTrue((Int64)mirror.GetValue("p").Payload == 6);
        }

        [Test]
        public void NestedBlockInFunction04()
        {
            String code =
@"
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Assert.IsTrue((Int64)mirror.GetValue("p").Payload == 6);
        }

        [Test]
        public void AccessGlobalVariableInsideFunction()
        {
            string code = @"
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Assert.IsTrue((Int64)mirror.GetValue("w0").Payload == 100);
            Assert.IsTrue((Int64)mirror.GetValue("w1").Payload == 200);
            Assert.IsTrue((Int64)mirror.GetValue("w2").Payload == 300);
        }


        [Test]
        public void Nestedblocks_Inside_1467358()
        {
            string code = @"
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Assert.IsTrue((Int64)mirror.GetValue("p1").Payload == 6);
            Assert.IsTrue((Int64)mirror.GetValue("p2").Payload == 6);

        }

        [Test]
        public void Nestedblocks_Inside_1467358_2()
        {
            string code = @"
            ProtoScript.Runners.ProtoScriptTestRunner fsr = new ProtoScript.Runners.ProtoScriptTestRunner();
            ExecutionMirror mirror = fsr.Execute(code, core);
            Assert.IsTrue((Int64)mirror.GetValue("p1").Payload == 6);
            Assert.IsTrue((Int64)mirror.GetValue("p2").Payload == 6);
        }
    }
}