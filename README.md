# CodeMap (ex-PyMap)
It is preferred that you installed the extension via [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=OlegShilo.PyMap-2017).
This extension is built for VS2022. Though you can download VS2019 version from the [Releases page](https://github.com/oleg-shilo/PyMap.VSIX/releases).
For older (pre VS2017) releases please visit [https://github.com/oleg-shilo/Retired-VSIX](https://github.com/oleg-shilo/Retired-VSIX).

---

This simple extension visualizes the code DOM objects defined in the active document. This extension is a for of the popular plugin that is available for:
* Sublime Text 3 - [Sublime CodeMap plugin](https://github.com/oleg-shilo/sublime-codemap/blob/master/README.md)
* Notepad++ - [Part of CS-Script.Npp plugin](https://github.com/oleg-shilo/cs-script.npp/blob/master/README.md)
* VS Code - [CodeMap](https://marketplace.visualstudio.com/items?itemName=oleg-shilo.codemap)

## Overview

_Code Tree viewer for Python and C# source code._

Historically, this extension was created to address the absence of the code tree/map view functionality for "Python Tools for Visual Studio" (PTVS). Thus some users can remember this tool by the name of PyMap. Starting from v2.0 it has been extended with the support for C# and has been renamed into CodeMap.

The usage is straight forward and dead simple. Open any C# or Python file and PyMap will automatically build a code tree. This tree will be automatically updated when the active VS document is saved or another document tab activated.

Clicking the item in the code tree will navigate to the location of the code element in the document.

## How is it different to the other code structure visualization tools

Well, it is different. While there are some very solid tools of this sort available they are focusing on teh different aspects of the user experience comparing to CodeMap.

- CodeMap alternatives (e.g. CodeMaid) are usually all about accuracy and completeness of the information being visualized. They are investing heavily into rendering as much information as possible and even providing some refactoring functionality. Basically "let's give the user as much information as he/she can possibly need.".

- CodeMap on another hand has only one objective - navigation. Usability of code navigation, if to be more precise. CodeMap is trying not to stay on your way and to do only a single job but to do it well and in the most ergonomic way.

As a developer, when I want to jump to the code where a certain algorithm is implemented. I am interested in the location of the code in the file and I want to get there in a single step. I am not interested so many things that are important in general but irrelevant right now:

- what is the return type of my method
- is it static or not
- is it public
- what is the complexity index of the method

When I use code map to navigate to the code:

- I am not interested in the location of the fields. Almost never. They have no behavior, only state.
- I am not interested in the properties if it is a model class. Particularly if the model is auto-generated.
- I am not interested in collapsing code tree nodes for the not important classes and then do ing it again and again simply because I have switched between the code file.
- I do not want to do refactoring (moving members around) from CodeMap. I have better tools for that.
- I do nopt want to explore relationships between a code element and its callers. I have better tools for that (e.g. Find All References).
- I do not want to be limited with my code tree visualization for thr current soultion files only. Even if the file does not belong to the solution (e.g. decompiled source on F12 - 'Go To Definition') I also wand to be able to navigate in this file freely.

One may ask "Why then not just allow the extra flexibility in some of the existing products, instead of maintaining 'yet another one'?". This is where it becomes more complicated.

I have contacted the owned of CodeMap and tried to contribute some this flexibility to the excellent CodeMaid. I did it twice. But amy PRs were not accepted. I am not complaining as avery OpenSource product author is absolutely entitled to have his/her own vision of the product evolution. Even if the proposed changes are not about changing the product but only about extra customization. Thus I have taken my older extension and improved it to meet the my own and hopefully other people expectations. And of course I am fully open to the suggestions regarding the extension functionality.

### CodeMap - C#

![](docs/Preview.png)

You can filter code tree content by the class or member name.
You can control inclusion of the members by their visibility (private/public) for methods, properties and fields.
You can choose to sort members of the class.

### CodeMap - Python

![](docs/Preview.py.png)

You can filter code tree content by the class member name.

_Tips_

* If you want to refresh the code tree just save the active document (Ctrl+S) and it will trigger the update.
* CodeMap window can be activated via _View->Other Windows->CodeMap_ menu:
  ![](docs/menu.png)
