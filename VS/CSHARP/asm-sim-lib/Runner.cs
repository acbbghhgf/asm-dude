﻿// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using AsmSim.Mnemonics;
using AsmTools;
using Microsoft.Z3;
using System;
using System.Collections.Generic;

namespace AsmSim
{
    public static class Runner
    {
        public static DynamicFlow Construct_DynamicFlow_Forward(
            StaticFlow flow,
            int startLineNumber,
            int maxSteps,
            Tools tools)
        {
            if (!flow.HasLine(startLineNumber))
            {
                if (!tools.Quiet) Console.WriteLine("WARNING: Construct_DynamicFlow2_Forward: startLine " + startLineNumber + " does not exist in " + flow);
                return null;
            }
            var nextKeys = new Stack<string>();

            // Get the head of the current state, this head will be the prevKey of the update, nextKey is fresh. 
            // When state is updated, tail is not changed; head is set to the fresh nextKey.

            #region Create the Root node
            DynamicFlow dFlow;
            {
                string rootKey = flow.Get_Key(startLineNumber);
                nextKeys.Push(rootKey);
                dFlow = new DynamicFlow(rootKey, tools);
                dFlow.Add_Vertex(rootKey, startLineNumber, 0);
            }
            #endregion

            while (nextKeys.Count > 0)
            {
                string prevKey = nextKeys.Pop();
                int step = dFlow.Step(prevKey);
                if (step <= maxSteps)
                {
                    int currentLineNumber = dFlow.LineNumber(prevKey);
                    if (flow.HasLine(currentLineNumber))
                    {
                        var nextLineNumber = flow.Get_Next_LineNumber(currentLineNumber);
                        (string nextKey, string nextKeyBranch) = flow.Get_Key(nextLineNumber);

                        var updates = Execute(flow, currentLineNumber, (prevKey, nextKey, nextKeyBranch), tools);
                        int nextStep = step + 1;

                        HandleBranch_LOCAL(currentLineNumber, nextLineNumber.Branch, nextStep, updates.Branch, prevKey, nextKeyBranch);
                        HandleRegular_LOCAL(currentLineNumber, nextLineNumber.Regular, nextStep, updates.Regular, prevKey, nextKey);
                    }
                }
            }
            #region Local Methods
            void HandleBranch_LOCAL(int currentLineNumber, int nextLineNumber, int nextStep, StateUpdate update, string prevKey, string nextKey)
            {
                if (update != null)
                {
                    if (nextLineNumber == -1)
                    {
                        Console.WriteLine("WARNING: Runner:Construct_DynamicFlow_Forward: according to flow there does not exists a branch yet a branch is computed");
                        return;
                    }
                    if (!dFlow.Has_Edge(prevKey, nextKey, true))
                    {
                        dFlow.Add_Vertex(nextKey, nextLineNumber, nextStep);
                        dFlow.Add_Edge(true, update, prevKey, nextKey);
                        nextKeys.Push(nextKey);

                        #region Display
                        if (!tools.Quiet) Console.WriteLine("=====================================");
                        if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Forward: stepNext " + nextStep + ": LINE " + currentLineNumber + ": \"" + flow.Get_Line_Str(currentLineNumber) + "\" Branches to LINE " + nextLineNumber);
                        if (!tools.Quiet && flow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Forward: " + update);
                        if (!tools.Quiet && flow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: " + dFlow.State_After(nextKey));
                        #endregion
                    }
                }
                else if (nextLineNumber != -1)
                {
                    Console.WriteLine("WARNING: Runner:Construct_DynamicFlow_Forward: according to flow there exists a branch yet no branch is computed");
                    return;
                }
            }
            void HandleRegular_LOCAL(int currentLineNumber, int nextLineNumber, int nextStep, StateUpdate update, string prevKey, string nextKey)
            {
                if (update != null)
                {
                    if (nextLineNumber == -1)
                    {
                        Console.WriteLine("WARNING: Runner:Construct_DynamicFlow_Forward: according to flow there does not exists a continueyet a continue is computed");
                        throw new Exception();
                    }
                    if (!dFlow.Has_Edge(prevKey, nextKey, false))
                    {
                        dFlow.Add_Vertex(nextKey, nextLineNumber, nextStep);
                        dFlow.Add_Edge(false, update, prevKey, nextKey);
                        nextKeys.Push(nextKey);

                        #region Display
                        if (!tools.Quiet) Console.WriteLine("=====================================");
                        if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Forward: stepNext " + nextStep + ": LINE " + currentLineNumber + ": \"" + flow.Get_Line_Str(currentLineNumber) + "\" Continues to LINE " + nextLineNumber);
                        if (!tools.Quiet && flow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Forward: " + update);
                        if (!tools.Quiet && flow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: " + dFlow.State_After(nextKey));
                        #endregion
                    }
                }
                else if (nextLineNumber != -1)
                {
                    Console.WriteLine("WARNING: Runner:Construct_DynamicFlow_Forward: according to flow there exists a regular continue yet no continue is computed");
                    throw new Exception();
                }
            }
            #endregion
            return dFlow;
        }

        public static DynamicFlow Construct_DynamicFlow_Backward(
            StaticFlow flow,
            int startLineNumber,
            int maxSteps,
            Tools tools)
        {
            if (!flow.Has_Prev_LineNumber(startLineNumber))
            {
                if (!tools.Quiet) Console.WriteLine("WARNING: Construct_DynamicFlow_Backward: startLine " + startLineNumber + " does not exist in " + flow);
                return null;
            }

            var prevKeys = new Stack<string>();

            // Get the tail of the current state, this tail will be the nextKey, the prevKey is fresh.
            // When the state is updated, the head is unaltered, tail is set to the fresh prevKey.

            #region Create the Root node
            string rootKey = flow.Get_Key(startLineNumber);

            DynamicFlow dFlow;
            {
                prevKeys.Push(rootKey);
                dFlow = new DynamicFlow(rootKey, tools);
                dFlow.Add_Vertex(rootKey, startLineNumber, 0);
            }
            #endregion

            while (prevKeys.Count > 0)
            {
                string nextKey = prevKeys.Pop();

                int step = dFlow.Step(nextKey);
                if (step <= maxSteps)
                {
                    int nextStep = step + 1;
                    int currentLineNumber = dFlow.LineNumber(nextKey);

                    foreach (var prev in flow.Get_Prev_LineNumber(currentLineNumber))
                    {
                        if (flow.HasLine(prev.LineNumber))
                        {
                            string prevKey = flow.Get_Key(prev.LineNumber);
                            if (!dFlow.Has_Edge(prevKey, nextKey, prev.IsBranch))
                            {
                                var updates = Runner.Execute(flow, prev.LineNumber, (prevKey, nextKey, nextKey), tools);
                                var update = (prev.IsBranch) ? updates.Branch : updates.Regular;

                                dFlow.Add_Vertex(prevKey, prev.LineNumber, nextStep);
                                dFlow.Add_Edge(prev.IsBranch, update, prevKey, nextKey);
                                Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Backward: scheduling key " + prevKey);
                                prevKeys.Push(prevKey); // only continue if the state is consistent; no need to go futher in the past if the state is inconsistent.

                                #region Display
                                if (!tools.Quiet) Console.WriteLine("=====================================");
                                if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Backward: stepNext " + nextStep + ": LINE " + prev.LineNumber + ": \"" + flow.Get_Line_Str(prev.LineNumber) + "; branch=" + prev.IsBranch);
                                if (!tools.Quiet && flow.Get_Line(prev.LineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Backward: " + update);
                                //if (!tools.Quiet && flow.GetLine(prev_LineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: " + stateTree.State_After(rootKey));
                                #endregion
                            }
                        }
                    }
                }
            }
            return dFlow;
        }

        /// <summary>Perform one step forward and return the regular branch</summary>
        public static State SimpleStep_Forward(string line, State state)
        {
            string nextKey = Tools.CreateKey(state.Tools.Rand);
            string nextKeyBranch = nextKey;// + "!B";
            var content = AsmSourceTools.ParseLine(line);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, (state.HeadKey, nextKey, nextKeyBranch), state.Tools);
            if (opcodeBase == null) return null;
            if (opcodeBase.IsHalted) return null;

            opcodeBase.Execute();
            State stateOut = new State(state);
            stateOut.Update_Forward(opcodeBase.Updates.Regular);
            if (!state.Tools.Quiet) Console.WriteLine("INFO: Runner:SimpleStep_Forward: after \"" + line + "\" we know:");
            if (!state.Tools.Quiet) Console.WriteLine(stateOut);
            return stateOut;
        }

        /// <summary>Perform onestep forward and return the state of the regular branch</summary>
        public static State SimpleStep_Backward(string line, State state)
        {
            string prevKey = Tools.CreateKey(state.Tools.Rand);
            string prevKeyBranch = prevKey;// + "!B";
            var content = AsmSourceTools.ParseLine(line);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, (prevKey, state.TailKey, state.TailKey), state.Tools);
            if (opcodeBase == null) return null;
            if (opcodeBase.IsHalted) return null;

            opcodeBase.Execute();
            State stateOut = new State(state);
            stateOut.Update_Backward(opcodeBase.Updates.Regular, prevKey);
            if (!state.Tools.Quiet) Console.WriteLine("INFO: Runner:SimpleStep_Backward: after \"" + line + "\" we know:");
            if (!state.Tools.Quiet) Console.WriteLine(stateOut);
            return stateOut;
        }

        /// <summary>Perform one step forward and return states for both branches</summary>
        public static (State Regular, State Branch) Step_Forward(string line, State state)
        {
            string nextKey = Tools.CreateKey(state.Tools.Rand);
            string nextKeyBranch = nextKey;// + "!B";
            var content = AsmSourceTools.ParseLine(line);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, (state.HeadKey, nextKey, nextKeyBranch), state.Tools);
            if (opcodeBase == null) return (Regular: null, Branch: null);
            if (opcodeBase.IsHalted) return (Regular: null, Branch: null);

            opcodeBase.Execute();
            State stateRegular = null;
            if (opcodeBase.Updates.Regular != null)
            {
                stateRegular = new State(state);
                stateRegular.Update_Forward(opcodeBase.Updates.Regular);
            }
            State stateBranch = null;
            if (opcodeBase.Updates.Branch != null)
            {
                stateBranch = new State(state);
                stateBranch.Update_Forward(opcodeBase.Updates.Branch);
            }
            return (Regular: stateRegular, Branch: stateBranch);
        }

        public static (StateUpdate Regular, StateUpdate Branch) Execute(
            StaticFlow flow,
            int lineNumber,
            (string prevKey, string nextKey, string nextKeyBranch) keys,
            Tools tools)
        {
            var content = flow.Get_Line(lineNumber);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, keys, tools);
            if (opcodeBase == null) return (Regular: null, Branch: null);
            if (opcodeBase.IsHalted) return (Regular: null, Branch: null);

            opcodeBase.Execute();
            return opcodeBase.Updates;
        }

        /// <summary>Get the branch condition for the provided lineNumber</summary>
        public static (BoolExpr Regular, BoolExpr Branch) GetBranchCondition(
            StaticFlow flow,
            int lineNumber,
            (string prevKey, string nextKey, string nextKeyBranch) keys,
            Tools tools)
        {
            var content = flow.Get_Line(lineNumber);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, keys, tools);
            if (opcodeBase == null) return (Regular: null, Branch: null);

            throw new NotImplementedException();
        }

        public static StateConfig GetUsage_StateConfig(
            StaticFlow flow,
            int lineNumberBegin,
            int lineNumberEnd,
            Tools tools)
        {
            StateConfig config = new StateConfig();
            config.Set_All_Off();
            var usage = GetUsage(flow, 0, flow.LastLineNumber, tools);
            config.Set_Flags_On(usage.Flags);
            foreach (Rn reg in usage.Regs) config.Set_Reg_On(reg);
            config.mem = usage.Mem;
            return config;
        }

        public static (ISet<Rn> Regs, Flags Flags, bool Mem) GetUsage(
            StaticFlow flow,
            int lineNumberBegin,
            int lineNumberEnd,
            Tools tools)
        {
            ISet<Rn> regs = new HashSet<Rn>();
            Flags flags = Flags.NONE;
            bool mem = false;
            var dummyKeys = ("", "", "");
            for (int lineNumber = lineNumberBegin; lineNumber <= lineNumberEnd; lineNumber++)
            {
                var content = flow.Get_Line(lineNumber);
                var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, dummyKeys, tools);
                if (opcodeBase != null)
                {
                    flags |= (opcodeBase.FlagsReadStatic | opcodeBase.FlagsWriteStatic);
                    foreach (Rn r in opcodeBase.RegsReadStatic) regs.Add(RegisterTools.Get64BitsRegister(r));
                    foreach (Rn r in opcodeBase.RegsWriteStatic) regs.Add(RegisterTools.Get64BitsRegister(r));
                    mem |= opcodeBase.MemReadWriteStatic;
                }
            }
            return (Regs: regs, Flags: flags, Mem: mem);
        }

        public static OpcodeBase InstantiateOpcode(
            Mnemonic mnemonic,
            string[] args,
            (string prevKey, string nextKey, string nextKeyBranch) keys,
            Tools t)
        {
            switch (mnemonic)
            {
                #region NonSse
                case Mnemonic.NONE:
                case Mnemonic.UNKNOWN: return new Ignore(mnemonic, args, keys, t);
                case Mnemonic.MOV: return new Mov(args, keys, t);
                case Mnemonic.CMOVE:
                case Mnemonic.CMOVZ:
                case Mnemonic.CMOVNE:
                case Mnemonic.CMOVNZ:
                case Mnemonic.CMOVA:
                case Mnemonic.CMOVNBE:
                case Mnemonic.CMOVAE:
                case Mnemonic.CMOVNB:
                case Mnemonic.CMOVB:
                case Mnemonic.CMOVNAE:
                case Mnemonic.CMOVBE:
                case Mnemonic.CMOVNA:
                case Mnemonic.CMOVG:
                case Mnemonic.CMOVNLE:
                case Mnemonic.CMOVGE:
                case Mnemonic.CMOVNL:
                case Mnemonic.CMOVL:
                case Mnemonic.CMOVNGE:
                case Mnemonic.CMOVLE:
                case Mnemonic.CMOVNG:
                case Mnemonic.CMOVC:
                case Mnemonic.CMOVNC:
                case Mnemonic.CMOVO:
                case Mnemonic.CMOVNO:
                case Mnemonic.CMOVS:
                case Mnemonic.CMOVNS:
                case Mnemonic.CMOVP:
                case Mnemonic.CMOVPE:
                case Mnemonic.CMOVNP:
                case Mnemonic.CMOVPO: return new Cmovcc(mnemonic, args, ToolsAsmSim.GetCe(mnemonic), keys, t);

                case Mnemonic.XCHG: break;
                case Mnemonic.BSWAP: break;
                case Mnemonic.XADD: return new Xadd(args, keys, t);
                case Mnemonic.CMPXCHG: break;
                case Mnemonic.CMPXCHG8B: break;
                case Mnemonic.PUSH: return new Push(args, keys, t);
                case Mnemonic.POP: return new Pop(args, keys, t);
                case Mnemonic.PUSHA: break;
                case Mnemonic.PUSHAD: break;
                case Mnemonic.POPA: break;
                case Mnemonic.POPAD: break;
                case Mnemonic.CWD: return new Cwd(args, keys, t);
                case Mnemonic.CDQ: return new Cdq(args, keys, t);
                case Mnemonic.CBW: return new Cbw(args, keys, t);
                case Mnemonic.CWDE: return new Cwde(args, keys, t);
                case Mnemonic.CQO: return new Cqo(args, keys, t);
                case Mnemonic.MOVSX: return new Movsx(args, keys, t);
                case Mnemonic.MOVSXD: return new Movsxd(args, keys, t);
                case Mnemonic.MOVZX: return new Movzx(args, keys, t);
                //case Mnemonic.MOVZXD: return new Movzxd(args, keys, t);
                case Mnemonic.ADCX: return new Adcx(args, keys, t);
                case Mnemonic.ADOX: return new Adox(args, keys, t);
                case Mnemonic.ADD: return new Add(args, keys, t);
                case Mnemonic.ADC: return new Adc(args, keys, t);
                case Mnemonic.SUB: return new Sub(args, keys, t);
                case Mnemonic.SBB: return new Sbb(args, keys, t);
                case Mnemonic.IMUL: return new Imul(args, keys, t);
                case Mnemonic.MUL: return new Mul(args, keys, t);
                case Mnemonic.IDIV: return new Idiv(args, keys, t);
                case Mnemonic.DIV: return new Div(args, keys, t);
                case Mnemonic.INC: return new Inc(args, keys, t);
                case Mnemonic.DEC: return new Dec(args, keys, t);
                case Mnemonic.NEG: return new Neg(args, keys, t);
                case Mnemonic.CMP: return new Cmp(args, keys, t);

                case Mnemonic.DAA: break;
                case Mnemonic.DAS: break;
                case Mnemonic.AAA: break;
                case Mnemonic.AAS: break;
                case Mnemonic.AAM: break;
                case Mnemonic.AAD: break;

                case Mnemonic.AND: return new And(args, keys, t);
                case Mnemonic.OR: return new Or(args, keys, t);
                case Mnemonic.XOR: return new Xor(args, keys, t);
                case Mnemonic.NOT: return new Not(args, keys, t);
                case Mnemonic.SAR: return new Sar(args, keys, t);
                case Mnemonic.SHR: return new Shr(args, keys, t);
                case Mnemonic.SAL: return new Sal(args, keys, t);
                case Mnemonic.SHL: return new Shl(args, keys, t);
                case Mnemonic.SHRD: return new Shrd(args, keys, t);
                case Mnemonic.SHLD: return new Shld(args, keys, t);
                case Mnemonic.ROR: return new Ror(args, keys, t);
                case Mnemonic.ROL: return new Rol(args, keys, t);
                case Mnemonic.RCR: return new Rcr(args, keys, t);
                case Mnemonic.RCL: return new Rcl(args, keys, t);

                case Mnemonic.BT: return new Bt_Opcode(args, keys, t);
                case Mnemonic.BTS: return new Bts(args, keys, t);
                case Mnemonic.BTR: return new Btr(args, keys, t);
                case Mnemonic.BTC: return new Btc(args, keys, t);
                case Mnemonic.BSF: return new Bsf(args, keys, t);
                case Mnemonic.BSR: return new Bsr(args, keys, t);
                case Mnemonic.TEST: return new Test(args, keys, t);
                case Mnemonic.CRC32: break;//return new Crc32(args, keys, t);

                case Mnemonic.SETE:
                case Mnemonic.SETZ:
                case Mnemonic.SETNE:
                case Mnemonic.SETNZ:
                case Mnemonic.SETA:
                case Mnemonic.SETNBE:
                case Mnemonic.SETAE:
                case Mnemonic.SETNB:
                case Mnemonic.SETNC:
                case Mnemonic.SETB:
                case Mnemonic.SETNAE:
                case Mnemonic.SETC:
                case Mnemonic.SETBE:
                case Mnemonic.SETNA:
                case Mnemonic.SETG:
                case Mnemonic.SETNLE:
                case Mnemonic.SETGE:
                case Mnemonic.SETNL:
                case Mnemonic.SETL:
                case Mnemonic.SETNGE:
                case Mnemonic.SETLE:
                case Mnemonic.SETNG:
                case Mnemonic.SETS:
                case Mnemonic.SETNS:
                case Mnemonic.SETO:
                case Mnemonic.SETNO:
                case Mnemonic.SETPE:
                case Mnemonic.SETP:
                case Mnemonic.SETNP:
                case Mnemonic.SETPO: return new Setcc(mnemonic, args, ToolsAsmSim.GetCe(mnemonic), keys, t);

                case Mnemonic.JMP: return new Jmp(args, keys, t);

                case Mnemonic.JE:
                case Mnemonic.JZ:
                case Mnemonic.JNE:
                case Mnemonic.JNZ:
                case Mnemonic.JA:
                case Mnemonic.JNBE:
                case Mnemonic.JAE:
                case Mnemonic.JNB:
                case Mnemonic.JB:
                case Mnemonic.JNAE:
                case Mnemonic.JBE:
                case Mnemonic.JNA:
                case Mnemonic.JG:
                case Mnemonic.JNLE:
                case Mnemonic.JGE:
                case Mnemonic.JNL:
                case Mnemonic.JL:
                case Mnemonic.JNGE:
                case Mnemonic.JLE:
                case Mnemonic.JNG:
                case Mnemonic.JC:
                case Mnemonic.JNC:
                case Mnemonic.JO:
                case Mnemonic.JNO:
                case Mnemonic.JS:
                case Mnemonic.JNS:
                case Mnemonic.JPO:
                case Mnemonic.JNP:
                case Mnemonic.JPE:
                case Mnemonic.JP:
                case Mnemonic.JCXZ:
                case Mnemonic.JECXZ:
                case Mnemonic.JRCXZ: return new Jmpcc(mnemonic, args, ToolsAsmSim.GetCe(mnemonic), keys, t);

                case Mnemonic.LOOP: return new Loop(args, keys, t);
                case Mnemonic.LOOPZ: return new Loopz(args, keys, t);
                case Mnemonic.LOOPE: return new Loope(args, keys, t);
                case Mnemonic.LOOPNZ: return new Loopnz(args, keys, t);
                case Mnemonic.LOOPNE: return new Loopne(args, keys, t);

                case Mnemonic.CALL: break;// return new Call(args, keys, t);
                case Mnemonic.RET: break; // return new Ret(args, keys, t);
                case Mnemonic.IRET: break;
                case Mnemonic.INT: break;
                case Mnemonic.INTO: break;
                case Mnemonic.BOUND: break;
                case Mnemonic.ENTER: break;
                case Mnemonic.LEAVE: break;
                case Mnemonic.MOVS: break;
                case Mnemonic.MOVSB: break;
                case Mnemonic.MOVSW: break;
                case Mnemonic.MOVSD: break;
                case Mnemonic.CMPS: break;
                case Mnemonic.CMPSB: break;
                case Mnemonic.CMPSW: break;
                case Mnemonic.CMPSD: break;
                case Mnemonic.SCAS: break;
                case Mnemonic.SCASB: break;
                case Mnemonic.SCASW: break;
                case Mnemonic.SCASD: break;
                case Mnemonic.LODS: break;
                case Mnemonic.LODSB: break;
                case Mnemonic.LODSW: break;
                case Mnemonic.LODSD: break;
                case Mnemonic.STOS: break;
                case Mnemonic.STOSB: break;
                case Mnemonic.STOSW: break;
                case Mnemonic.STOSD: break;
                case Mnemonic.REP: break;
                case Mnemonic.REPE: break;
                case Mnemonic.REPZ: break;
                case Mnemonic.REPNE: break;
                case Mnemonic.REPNZ: break;
                case Mnemonic.IN: return new In(args, keys, t);
                case Mnemonic.OUT: return new Out(args, keys, t);
                case Mnemonic.INS: break;
                case Mnemonic.INSB: break;
                case Mnemonic.INSW: break;
                case Mnemonic.INSD: break;
                case Mnemonic.OUTS: break;
                case Mnemonic.OUTSB: break;
                case Mnemonic.OUTSW: break;
                case Mnemonic.OUTSD: break;
                case Mnemonic.STC: return new Stc(args, keys, t);
                case Mnemonic.CLC: return new Clc(args, keys, t);
                case Mnemonic.CMC: return new Cmc(args, keys, t);
                case Mnemonic.CLD: break;
                case Mnemonic.STD: break;
                case Mnemonic.LAHF: return new Lahf(args, keys, t);
                case Mnemonic.SAHF: return new Sahf(args, keys, t);
                case Mnemonic.PUSHF: break;
                case Mnemonic.PUSHFD: break;
                case Mnemonic.POPF: break;
                case Mnemonic.POPFD: break;
                case Mnemonic.STI: break;
                case Mnemonic.CLI: break;
                case Mnemonic.LDS: break;
                case Mnemonic.LES: break;
                case Mnemonic.LFS: break;
                case Mnemonic.LGS: break;
                case Mnemonic.LSS: break;
                case Mnemonic.LEA: return new Lea(args, keys, t);
                case Mnemonic.NOP: return new Nop(args, keys, t);
                case Mnemonic.UD2: return new Nop(args, keys, t);
                case Mnemonic.XLAT: break;
                case Mnemonic.XLATB: break;
                case Mnemonic.CPUID: break;
                case Mnemonic.MOVBE: break;
                case Mnemonic.PREFETCHW: return new Nop(args, keys, t);
                case Mnemonic.PREFETCHWT1: return new Nop(args, keys, t);
                case Mnemonic.CLFLUSH: return new Nop(args, keys, t);
                case Mnemonic.CLFLUSHOPT: return new Nop(args, keys, t);
                case Mnemonic.XSAVE: break;
                case Mnemonic.XSAVEC: break;
                case Mnemonic.XSAVEOPT: break;
                case Mnemonic.XRSTOR: break;
                case Mnemonic.XGETBV: break;
                case Mnemonic.RDRAND: break;
                case Mnemonic.RDSEED: break;
                case Mnemonic.ANDN: break;
                case Mnemonic.BEXTR: break;
                case Mnemonic.BLSI: break;
                case Mnemonic.BLSMSK: break;
                case Mnemonic.BLSR: break;
                case Mnemonic.BZHI: break;
                case Mnemonic.LZCNT: break;
                case Mnemonic.MULX: break;
                case Mnemonic.PDEP: break;
                case Mnemonic.PEXT: break;
                case Mnemonic.RORX: return new Rorx(args, keys, t);
                case Mnemonic.SARX: return new Sarx(args, keys, t);
                case Mnemonic.SHLX: return new Shlx(args, keys, t);
                case Mnemonic.SHRX: return new Shrx(args, keys, t);
                case Mnemonic.TZCNT: break;

                #endregion NonSse

                #region SSE
                //case Mnemonic.ADDPD: return new AddPD(args, keys, t); 
                case Mnemonic.POPCNT: return new Popcnt(args, keys, t);

                #endregion SSE

                default: return new NotImplemented(mnemonic, args, keys, t);
            }
            return new NotImplemented(mnemonic, args, keys, t);
        }
    }
}