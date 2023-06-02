from interpreter import Interpreter

f = open("main.free", "r")
program = f.readlines()
f.close()

interpreter = Interpreter(program)
interpreter.run()