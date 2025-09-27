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

                class NakedRootClass
                {
                }

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

            var items = map
                .Concat(map.SelectMany(x => x.Children))
                .Select(x => new { x.Id, Item = x }).ToArray();

            var idList = items.Select(x => x.Id).ToArray();

            Assert.Contains("<global>", idList);
            Assert.Contains("NakedRootClass", idList);
            Assert.Contains(".RootMethod()", idList);
            Assert.Contains(".#start: NestedTypesRegion", idList);
            Assert.Contains(".#end: NestedTypesRegion", idList);
            Assert.Contains(".#start: NestedStructRegion", idList);
            Assert.Contains(".#end: NestedStructRegion", idList);
            Assert.Contains("AppNamespace|RootClass.NestedEnum", idList);
            Assert.Contains("AppNamespace|RootClass.NestedClass", idList);
            Assert.Contains("AppNamespace|RootClass.NestedStruct", idList);
            Assert.Contains("AppNamespace|RootClass.NestedInterface", idList);
            Assert.Contains("AppNamespace|RootClass", idList);
            Assert.Contains("AppNamespace|RootClass.RootClass()", idList);
            Assert.Contains("AppNamespace|RootClass.NestedClass.NestedFoo1()", idList);
            Assert.Contains("AppNamespace|RootClass.NestedStruct.NestedFoo()", idList);
            Assert.Contains("AppNamespace|RootClass.NestedInterface.NestedFoo()", idList);
            Assert.Contains("AppNamespace|RootClass.Foo1()", idList);
            Assert.Contains("AppNamespace|RootClass.Foo1(int arg)", idList);
            Assert.Contains("AppNamespace|RootClass.Property1", idList);
            Assert.Contains("AppNamespace|RootClass.field1", idList);
        }

        [Fact]
        void EdgeCaseof_Issue_27()
        {
            var code = """
                using System;
                namespace AppNamespace;

                class RootClass
                {
                    int PropB { set; get; }

                    #region Fields

                    int FieldCB;
                    int FieldB;

                    #endregion Fields

                    int PropA { set; get; }
                }
                """;

            var file = code.ToTestInputFile();

            var parser = new SyntaxParser();

            //---------------------
            parser.SortMembers = false; // no sorting so the items appear as they are in the file
            parser.GenerateMap(file);

            //---------------------
            var view = parser.MemberList.ToView();

            var expected = """
                RootClass
                    PropB
                    #start: Fields
                    FieldCB
                    FieldB
                    #end: Fields
                    PropA
                """;
            Assert.Equal(expected.Trim(), view.Trim());
        }

        [Fact]
        void Issue_29()
        {
            var code = """
                using System;
                namespace nsp1.nsp2.nsp3;

                class RootClass
                {
                    class NestedClass
                    {
                        int MethodA(){}
                        int MethodB(){}
                    }
                    int FieldA
                    int FieldB
                }
                """;

            var file = code.ToTestInputFile();

            var parser = new SyntaxParser();

            //---------------------
            parser.SortMembers = false; // no sorting so the items appear as they are in the file
            parser.GenerateMap(file);

            //---------------------
            var view = parser.MemberList.ToView();

            var expected = """
                RootClass
                    NestedClass
                        MethodA
                        MethodB
                    FieldA
                    FieldB
                """;
            Assert.Equal(expected.Trim(), view.Trim());
        }

        [Fact]
        void Can_support_nested_regions()
        {
            var code = """
                class RootClass
                {
                    int PropB { set; get; }

                    #region Fields

                    int FieldC;

                    #region Constants

                    int Id;

                    #endregion Constants

                    int FieldB;

                    #endregion Fields

                    int PropA { set; get; }
                }
                """;

            var file = code.ToTestInputFile();

            var parser = new SyntaxParser();

            //---------------------
            parser.SortMembers = false; // no sorting so the items appear as they are in the file
            parser.GenerateMap(file);

            //---------------------
            var view = parser.MemberList.ToView();

            var expected = """
                RootClass
                    PropB
                    #start: Fields
                    FieldC
                    #start: Constants
                    Id
                    #end: Constants
                    FieldB
                    #end: Fields
                    PropA
                """;
            Assert.Equal(expected.Trim(), view.Trim());
        }

        [Fact]
        void CanOrderItemsByLocation()
        {
            var code = """
                using System;
                namespace AppNamespace;

                class l0_class1
                {
                    int l0_class_Mem3 { get; set; }

                    class l1_class2
                    {
                        int l1_class2_Mem3 { get; set; }
                        int l1_class2_Mem2 { get; set; }
                        int l1_class2_Mem1 { get; set; }
                    }

                    class l1_class1
                    {
                        int l1_class1_Mem3 { get; set; }
                        int l1_class1_Mem2 { get; set; }

                        class l2_class1
                        {
                            int l2_class1_Mem3 { get; set; }
                            int l2_class1_Mem2 { get; set; }
                            int l2_class1_Mem1 { get; set; }
                        }
                        int l1_class1_Mem1 { get; set; }
                    }

                    int l0_class_Mem2 { get; set; }
                    int l0_class_Mem1 { get; set; }
                }
                """;

            var file = code.ToTestInputFile();

            var parser = new SyntaxParser();

            //---------------------
            parser.SortMembers = false; // no sorting so the items appear as they are in the file
            parser.GenerateMap(file);
            //---------------------
            var view = parser.MemberList.ToView();

            var expected = """
                l0_class1
                    l0_class_Mem3
                    l1_class2
                        l1_class2_Mem3
                        l1_class2_Mem2
                        l1_class2_Mem1
                    l1_class1
                        l1_class1_Mem3
                        l1_class1_Mem2
                        l2_class1
                            l2_class1_Mem3
                            l2_class1_Mem2
                            l2_class1_Mem1
                        l1_class1_Mem1
                    l0_class_Mem2
                    l0_class_Mem1
                """;
            Assert.Equal(expected.Trim(), view.Trim());
        }

        [Fact]
        void CanOrderItemsByName()
        {
            var code = """
                using System;
                namespace AppNamespace;

                class l0_class1
                {
                    int l0_class_Mem3 { get; set; }

                    class l1_class2
                    {
                        int l1_class2_Mem3 { get; set; }
                        int l1_class2_Mem2 { get; set; }
                        int l1_class2_Mem1 { get; set; }
                    }

                    class l1_class1
                    {
                        int l1_class1_Mem3 { get; set; }
                        int l1_class1_Mem2 { get; set; }

                        class l2_class1
                        {
                            int l2_class1_Mem3 { get; set; }
                            int l2_class1_Mem2 { get; set; }
                            int l2_class1_Mem1 { get; set; }
                        }
                        class al2_class1
                        {
                            int al2_class1_Mem3 { get; set; }
                            int al2_class1_Mem2 { get; set; }
                            int al2_class1_Mem1 { get; set; }
                        }
                        int l1_class1_Mem1 { get; set; }
                    }

                    int l0_class_Mem2 { get; set; }
                    int l0_class_Mem1 { get; set; }
                }
                """;

            var file = code.ToTestInputFile();

            var parser = new SyntaxParser();

            //---------------------
            parser.SortMembers = true; // no sorting so the items appear as they are in the file
            parser.GenerateMap(file);
            //---------------------
            var view = parser.MemberList.ToView();

            var expected = """
                l0_class1
                    l0_class_Mem1
                    l0_class_Mem2
                    l0_class_Mem3
                    l1_class1
                        l1_class1_Mem1
                        l1_class1_Mem2
                        l1_class1_Mem3
                        al2_class1
                            al2_class1_Mem1
                            al2_class1_Mem2
                            al2_class1_Mem3
                        l2_class1
                            l2_class1_Mem1
                            l2_class1_Mem2
                            l2_class1_Mem3
                    l1_class2
                        l1_class2_Mem1
                        l1_class2_Mem2
                        l1_class2_Mem3
                """;
            Assert.Equal(expected.Trim(), view.Trim());
        }
    }
}