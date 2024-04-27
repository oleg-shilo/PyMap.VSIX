namespace CodeMap.Test
{
    public class GenericTests
    {
        [Fact]
        public void AllMembersHaveDistinctIds()
        {
            var code = """

                using System;

                class Program
                {
                    static void Main()
                    {
                        Console.WriteLine(""Hello, World!"");
                    }

                    int Test()
                    {
                        return 0;
                    }

                    int Test(int arg)
                    {
                        return 0;
                    }

                    int Test(int arg, int arg2)
                    {
                        return 0;
                    }
                }

                """;

            var map = CSharpMapper.GenerateForCode(code, false);

            var idList = map.SelectMany(x => x.Children).Select(x => x.Id);

            Assert.Equal(4, idList.Distinct().Count());
        }
    }
}