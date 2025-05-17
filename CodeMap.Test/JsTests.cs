namespace CodeMap.Test
{
    public class JsTests
    {
        [Fact]
        public void OldStyleSyntax()
        {
            //whirte code to temp file and delete it after the test

            var code = """
                function foo() {
                    console.log("Hello, world!");
                }

                var bar = function() {
                    console.log("Hello, world!");
                };

                var baz = () => {
                    console.log("Hello, world!");
                };

                window.codemirrorInterop = {
                    init: function (theme, dotnetRef) {
                    }
                }

                window.codemirrorInterop.restoreSplitPositions = function () {
                }

                window.codemirrorInterop.utils.foo = function () {
                }

                class MyClass {
                    constructor() {
                        console.log("Hello, world!");
                    }

                    myMethod() {
                        console.log("Hello, world!");
                    }
                }

                class mapper {
                    read_all_lines(file: string): string[] {
                        let text = fs.readFileSync(file, 'utf8');
                        return text.split(/\r?\n/g);
                    }

                    static to_display_text(text: string): string {
                    }
                }
                """;

            var codeLines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            var map = JavaScriptMapper.Generate(codeLines, false);

            var items = map.Select(x => x).Concat(map.SelectMany(x => x.Children))
                .Select(x => new { x.Id, Item = x }).ToArray();

            var idList = items.Select(x => x.Id).ToArray();

            var structured = map.Structure();
        }
    }
}