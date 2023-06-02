import GraphicsEngine as ge

class Interpreter:
    def __init__(self, program):
        self.PC = 0
        self.program = program
        self.vars = {}
        self.if_stack = []
        self.while_stack = []
        self.for_stack = []
        self.objects_list = {"rect":ge.Rect, "oval":ge.Oval, "circle":ge.Circle, "line":ge.Line, "text":ge.Text, "entry":ge.Entry, "image":ge.Image}

    def run(self):
        while self.PC < len(self.program):
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

    def execute_command(self):
        line = self.program[self.PC]
        raw_args = line.split(" ")
        args = []
        for i in raw_args:
            i = i.replace("\n", "")
            args.append(i)
        cmd = args[0]
        args.pop(0)
        if (cmd.startswith("#")) or (cmd.startswith("--")) or (cmd.startswith("//")): return
        if cmd == "PRINT":
            text = ""
            for i in args:
                if "var:" in i:
                    i = self.vars[i.split(":")[1].replace("\n", "")]
                i = self.check_variable(i)
                text += str(i) + " "
            print(text)
        elif cmd == "VAR":
            if args[1] in self.objects_list:
                obj = self.check_object(args)
                self.vars[args[1]] = obj
            else:
                if args[1] == "INT":
                    self.vars[args[1]] = self.check_variable(args[2].replace("\n", ""))+'-free'
                else:
                    self.vars[args[1]] = self.check_variable(args[2].replace("\n", ""))
        elif cmd == "ADD":
            try:
                var_name = args[0]
                value = int(args[1])
                if (var_name in self.vars) and (self.vars[var_name].endswith("-free")):
                    self.vars[var_name] = self.vars[var_name].replace("-free", "")
                    self.vars[var_name] = f'{int(self.vars[var_name]) + int(value)}-free'
                else:
                    self.vars[var_name] = f'{value}-free'
            except TypeError:
                print("Cannot add two different types")
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
        elif cmd == "FOR":
            self.execute_command_block()
            self.skip_for_block()
        elif cmd == "END":
            if self.if_stack:
                self.PC = self.if_stack.pop()
            elif self.while_stack:
                self.PC = self.while_stack.pop() - 1
            elif self.for_stack:
                self.PC = self.for_stack[-1][3]
                self.update_for_loop()
        else:
            raise NotImplementedError(f"This command '{cmd}' is not valid or was not implemented yet")

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
      var1 = int(self.check_variable(var1).replace("-free", ""))
      var2 = int(self.check_variable(var2).replace("-free", ""))-1
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
                raise ValueError("Missing 'END' statement in the program")
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
                raise ValueError("Missing 'END' statement in the program")
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
        line = self.program[self.PC]
        nested_level = 1
        while nested_level > 0:
            self.PC += 1
            if self.PC >= len(self.program):
                raise ValueError("Missing 'END' statement in the program")
            line = self.program[self.PC]
            if line == "FOR" or line == "WHILE":
                nested_level += 1
            elif line == "END":
                nested_level -= 1
    
    def check_object(self, args):
        type = args[2]
        if type == "rect":
            obj = ge.Rect(args[3], args[4], args[5], args[6], args[7])
            return obj