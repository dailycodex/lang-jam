from interpreter import Interpreter
#import sys

#f = open(sys.argv[1], "r")
f = open("main.free", "r")
program = f.readlines()
f.close()

interpreter = Interpreter(program)
interpreter.run()