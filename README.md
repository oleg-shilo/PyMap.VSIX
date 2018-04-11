# PyMap-2017
This is a source code and defect tracking repository only. For all binaries please visit the product [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=OlegShilo.PyMap-2017) page.

---

**PyMap**

_The extension has been rebuild for VS2017 thus it may or may not work on older versions of VS. For older (pre VS2017) releases please visit [https://github.com/oleg-shilo/Retired-VSIX](https://github.com/oleg-shilo/Retired-VSIX)._

_CodeMap View for Python source code._

"Python Tools for Visual Studio" (PTVS) is a great extension that converts Visual Studio into a powerful hard to beat Python IDE. Though there is one feature that it was always missing: a _CodeMap._ A visual representation of the object structure for the active Python document.

Of course such a fundamental feature should be a part of any IDE just out of box but... despite numerous updates PTVS still has no CodeMap equivalent. PyMap is an attempt to fill this gap. Most likely PTVS team will eventually develop their own equivalent but until then PyMap will probably be the best option to go with.

The usage is straight forward and dead simple. Open any Python file and PyMap will automatically build a code tree. This tree will be automatically updated when the active VS document is saved or another document tab activated.

Double-clicking the item in the code tree will navigate to the location of the code element in the document.

![](docs/Preview.png)

_Tips_

* If you want to refresh the code tree just save the active document (Ctrl+S) and it will trigger the update.
* CodeMap window can be activated via _View->Other Windows->CodeMap Python_ menu:
  ![](docs/menu.png)
