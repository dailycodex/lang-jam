import GraphicsEngine as ge

class Interpreter:
    def __init__(self, program):
        self.PC = 0
        self.program = program
        self.vars = {}
        self.if_stack = []
        self.while_stack = []
        self.for_stack = []
        self.functions = {}
        self.objects_list = ["RECT", "OVAL", "CIRCLE", "LINE", "TEXT", "ENTRY", "IMAGE"]

    def run(self):
        self.window = ge.Window("FreeLang", 800, 600)
        while not self.window.is_closed():
            if self.PC < len(self.program):
                self.execute_command()
                if self.for_stack:
                    var_name, end, step, for_start_pc = self.for_stack[-1]
                    if self.vars[var_name] + step <= end:
                        self.vars[var_name] += step
                        self.PC = for_start_pc
                    else:
                        self.for_stack.pop() 
                else:
                    self.PC += 1
            else: self.execute_command("RUNFUNC _PROCESS")
            try: self.window.update()
            except: pass

    def execute_command(self, line=None):
        if line == None:
            line = self.program[self.PC]
        raw_args = line.split(" ")
        args = []
        for i in raw_args:
            i = i.replace("\n", "")
            args.append(i)
        cmd = args[0]
        if (cmd.startswith("#")) or (cmd.startswith("--")) or (cmd.startswith("//")): return
        args.pop(0)
        if cmd == "FOR":
            self.handle_for_loop(args)
        if cmd == "LOOP":
            self.execute_command_block()
        elif cmd == "END":
            if self.if_stack:
                self.PC = self.if_stack.pop()
            elif self.while_stack:
                self.PC = self.while_stack.pop() - 1
            elif self.for_stack:
                self.PC = self.for_stack[-1][3]
                self.update_for_loop()
            self.PC += 1
            print("ran a loop")
        elif cmd == "PRINT":
            text = ""
            for i in args:
                if i.startswith("VAR:"):
                    i = self.vars[i.split(":")[1].replace("\n", "")]
                    i = self.check_variable(i)
                text += str(i) + " "
            print(text)
        elif cmd == "VAR":
            if args[0] in self.objects_list:
                obj = self.check_object(args)
                self.vars[args[1]] = obj
            elif args[0] == "CLICK_CHECK":
                #try:
                x, y = self.window.get_click()
                self.vars[args[1]] = f'{int(self.vars[args[2]].is_touched(x, y))}-FREE'
                #except Exception as e:
                    #print(e)
            else:
                if args[1] == "INT":
                    self.vars[args[1]] = self.check_variable(args[2].replace("\n", ""))+'-FREE'
                else:
                    self.vars[args[1]] = self.check_variable(args[2].replace("\n", ""))
        elif cmd == "ADD":
            try:
                var_name = args[0]
                value = int(args[1])
                if (var_name in self.vars) and (self.vars[var_name].endswith("-FREE")):
                    self.vars[var_name] = self.vars[var_name].replace("-FREE", "")
                    self.vars[var_name] = f'{int(self.vars[var_name]) + int(value)}-FREE'
                else:
                    self.vars[var_name] = f'{value}-FREE'
            except TypeError:
                print("Cannot add two different types")
        elif cmd == "SUB":
            try:
                var_name = args[0]
                value = int(args[1])
                if (var_name in self.vars) and (self.vars[var_name].endswith("-FREE")):
                    self.vars[var_name] = self.vars[var_name].replace("-FREE", "")
                    self.vars[var_name] = f'{int(self.vars[var_name]) - int(value)}-FREE'
                else:
                    self.vars[var_name] = f'{value}-FREE'
            except TypeError:
                print("Cannot substract two different types")
        elif cmd == "MUL":
            try:
                var_name = args[0]
                value = int(args[1])
                if (var_name in self.vars) and (self.vars[var_name].endswith("-FREE")):
                    self.vars[var_name] = self.vars[var_name].replace("-FREE", "")
                    self.vars[var_name] = f'{int(self.vars[var_name]) * int(value)}-FREE'
                else:
                    self.vars[var_name] = f'{value}-FREE'
            except TypeError:
                print("Cannot multiply two different types")
        elif cmd == "DIV":
            try:
                var_name = args[0]
                value = int(args[1])
                if (var_name in self.vars) and (self.vars[var_name].endswith("-FREE")):
                    self.vars[var_name] = self.vars[var_name].replace("-FREE", "")
                    self.vars[var_name] = f'{int(self.vars[var_name]) / int(value)}-FREE'
                else:
                    self.vars[var_name] = f'{value}-FREE'
            except TypeError:
                print("Cannot divide two different types")
            except ZeroDivisionError:
                print("Cannot divide with 0")
        elif cmd == "FUNCTION":
            self.define_function(args)
            return
        elif cmd == "RUNFUNC":
            self.run_function(args)
            return
        elif cmd == "MOVE":
            var_name = args[0]
            dx = args[1]
            dy = args[2]
            obj = self.vars[var_name]
            try:
                obj.move(int(dx), int(dy))
            except:
                print("Cannot move object")
        #elif cmd == "CONFIG":
            #text = ""
            #for i in args[2:]: text=text+i+" "
            #self.window.config(args[0], args[1], text)
        elif cmd == "HIDE":
            var_name = args[0]
            obj = self.vars[var_name]
            try:
                obj.hide()
            except:
                print("Cannot hide object")
        elif cmd == "SHOW":
            var_name = args[0]
            obj = self.vars[var_name]
            try:
                obj.show()
            except:
                print("Cannot show object")
        elif cmd == "IF":
            if self.check_if(args[0], args[1], args[2]):
                self.if_stack.append(self.PC)
            else:
                self.skip_if_block()
        elif cmd == "WHILE":
            if self.check_if(args[0], args[1], args[2]):
                self.while_stack.append(self.PC)
            else:
                self.skip_while_block()
        else:
            raise NotImplementedError(f"This command '{cmd}' is not valid or was not implemented yet")
    
    def define_function(self, args):
        func_name = args[0]
        self.functions[func_name] = (self.PC)

    def run_function(self, args):
        func_name = args[0]
        if func_name in self.functions:
            func_start_pc = self.functions[func_name]
            saved_pc = self.PC
            self.PC = func_start_pc
            self.execute_command()
            self.PC = saved_pc
        else:
            raise ValueError(f"Function '{func_name}' is not defined.")

    def is_infinite_loop(self):
        line = self.program[self.PC]
        if line.strip() == "LOOP":
            return True
        return False

    def handle_for_loop(self, args):
        var_name = args[0]
        start = int(args[1])
        end = int(args[2])
        step = int(args[3])
        self.vars[var_name] = start
        self.for_stack.append((var_name, end, step, self.PC))
        self.execute_for_loop(var_name, start, end, step)

    def update_for_loop(self):
        var_name, end, step, for_start_pc = self.for_stack[-1]
        if self.vars[var_name] + step <= end:
            self.vars[var_name] += step
        else:
            self.for_stack.pop()

    def skip_for_block(self):
        nested_level = 1
        while nested_level > 0:
            self.PC += 1
            if self.PC >= len(self.program):
                raise ValueError("Missing 'END' statement in the program")
            line = self.program[self.PC]
            if line.strip() == "FOR":
                nested_level += 1
            elif line.strip() == "END":
                nested_level -= 1

    def is_variable(self, var):
        if var in self.vars:
            return True
        else:
            return False

    def check_variable(self, var):
        if var in self.vars: return self.vars[var]
        else: return var

    def check_if(self, var1, conditional, var2):
      var1 = int(self.check_variable(var1).replace("-FREE", ""))
      var2 = int(self.check_variable(var2).replace("-FREE", ""))-1
      if conditional == "==": return var1==var2
      elif conditional == "<": return var1<var2
      elif conditional == ">": return var1>var2
      elif conditional == "<=": return var1<=var2
      elif conditional == ">=": return var1>=var2
      else: return False

    def skip_if_block(self):
        nested_level = 1
        while nested_level > 0:
            self.PC += 1
            if self.PC >= len(self.program):
                raise ValueError("Missing END in program")
            line = self.program[self.PC]
            if line == "IF":
                nested_level += 1
            elif line == "END":
                nested_level -= 1

    def skip_while_block(self):
        nested_level = 1
        while nested_level > 0:
            self.PC += 1
            if self.PC >= len(self.program):
                raise ValueError("Missing END in program")
            line = self.program[self.PC]
            if line == "WHILE":
                nested_level += 1
            elif line == "END":
                nested_level -= 1
    
    def execute_for_loop(self, variable, start, end, step):
        start = int(start)
        end = int(end)
        step = int(step)
        for i in range(start, end, step):
            self.vars[variable] = str(i)
            self.execute_command_block()

    def execute_command_block(self):
        start_PC = self.PC + 1
        while True:
            try: 
                if self.window.is_closed(): break
            except: pass
            self.PC += 1
            if self.PC >= len(self.program):
                raise ValueError("Missing 'END' in program")
            line = self.program[self.PC]
            if line == "END":
                self.PC = start_PC
            if line == "BREAK":
                self.PC += 1
                break
            self.execute_command()

    
    def check_object(self, args):
        type = args[0]
        print(args)
        if type == "RECT": return ge.Rect(int(args[2]), int(args[3]), int(args[4]), int(args[5]), args[6])
        elif type == "OVAL": return ge.Oval(int(args[2]), int(args[3]), int(args[4]), int(args[5]), args[6])
        elif type == "CIRCLE": return ge.Circle(int(args[2]), int(args[3]), int(args[4]), args[5])
        elif type == "LINE": return ge.Line(int(args[2]), int(args[3]), int(args[4]), int(args[5]), args[6])
        elif type == "TEXT":
            text = ""
            for i in args[5:]: text+=i+" "
            return ge.Text(int(args[2]), int(args[3]), text, args[4])
        elif type == "ENTRY": return ge.Entry(int(args[2]), int(args[3]), int(args[4]), args[5])
        