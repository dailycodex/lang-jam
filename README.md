To run the project, you can:

- go to https://derkune.itch.io/langjam1984

- run one of the binaries in folders WindowsExport and LinuxExport

- Open with Godot 3.5.1 mono

Click Menu button at the top right corner for language intro and sample programs.

The name refers to a puzzle game I would have liked to make with it originally, themed around George Orwell's 1984. You'd solve puzzles with this language.

The idea behind language is self modifying code: interpreter seeks for statements in text, then applies these statements to the same text. The goal of puzzles is to edit text in some way for each puzzle.

For example: count the number of letter N in each paragraph and append it to the end of psrsgraphs (puzzle 1 in the linked program), or to negate every word by adding or removing "UN" for each word in given dictionary (puzzle 2 in the linked program)

The code is executed until all commands in the document have failed. Then it stops, and the puzzle's objective should have been completed.

Sadly, I wasnt speedy enough to make a self modifying program, run out of deadline time

But program statements can insert or even modify it other program statements.
