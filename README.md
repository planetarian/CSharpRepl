# CSharpRepl
A sort-of read-eval-print loop type thing for C#.

This is a simple console app which you can use similar to a basic repl.

The repl operates as though you are writing the body of an anonymous method.
As such, the code is checked for errors on every line, but not actually executed
until you provide a return statement.
```
> return "hello world!";
Output:
hello world!
```
### Execution

Everything in the .NET framework should be usable, and everything will execute
directly as written -- e.g. instances of Console.Write* will appear before the return.
```
> Console.WriteLine(1);
> return 2;
1
Output:
2
```
### Methods

You can also define methods which can be called by later code.
These methods are persistent for the current session.
```
> method int Add (int x, int y)
Add> return x+y;
Method Add defined.
> return Add(42,500);
Output:
542
```
### Errors

Each line is checked for compile-time errors and a message is shown upon detection.
If an error is encountered, the line containing the error will be ignored,
and you may continue from the previous statement.
```
> int foo = 1;
> foo = "2";
Compiler Error[s]:

      foo = "2";
  Col 13, Error CS0029: Cannot implicitly convert type 'string' to 'int'

> return foo;
Output:
1
```
