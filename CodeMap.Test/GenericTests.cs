namespace CodeMap.Test
{
    public class GenericTests
    {
        [Fact]
        public void CanGenerateProperItemIds()
        {
            var code = """

                using System;

                void RootMethod(){}

                namespace AppNamespace;

                class RootClass
                {
                    RootClass() {}

                    void Foo1() {}
                    void Foo1(int arg) {}

                    class NestedClass
                    {
                        void NestedFoo1() {}
                    }

                #region NestedTypesRegion

                    enum NestedEnum
                    {
                        EnumValue1,
                    }

                #region NestedStructRegion

                    struct NestedStruct
                    {
                        void NestedFoo() {}
                    }

                #endregion NestedStructRegion

                    interface NestedInterface
                    {
                        void NestedFoo();
                    }

                #endregion NestedTypesRegion

                    event EventHandler NestedEvent;

                    int Property1 { get; set; }

                    int field1;
                }
                """;

            var map = CSharpMapper.GenerateForCode(code, false);

            var items = map.Select(x => x).Concat(map.SelectMany(x => x.Children))
                .Select(x => new { x.Id, Item = x }).ToArray();

            var idList = items.Select(x => x.Id).ToArray();

            Assert.Contains(".<global>", idList);
            Assert.Contains(".RootMethod()", idList);
            Assert.Contains(".region: NestedTypesRegion", idList);
            Assert.Contains(".region: NestedStructRegion", idList);
            Assert.Contains("AppNamespace.RootClass.NestedEnum", idList);
            Assert.Contains("AppNamespace.RootClass.NestedClass", idList);
            Assert.Contains("AppNamespace.RootClass.NestedStruct", idList);
            Assert.Contains("AppNamespace.RootClass.NestedInterface", idList);
            Assert.Contains("AppNamespace.RootClass", idList);
            Assert.Contains("AppNamespace.RootClass.RootClass()", idList);
            Assert.Contains("AppNamespace.RootClass.NestedClass.NestedFoo1()", idList);
            Assert.Contains("AppNamespace.RootClass.NestedStruct.NestedFoo()", idList);
            Assert.Contains("AppNamespace.RootClass.NestedInterface.NestedFoo()", idList);
            Assert.Contains("AppNamespace.RootClass.Foo1()", idList);
            Assert.Contains("AppNamespace.RootClass.Foo1(int arg)", idList);
            Assert.Contains("AppNamespace.RootClass.Property1", idList);
            Assert.Contains("AppNamespace.RootClass.field1", idList);
        }
    }
}