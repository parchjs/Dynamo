﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Collections.ObjectModel;
using Dynamo.Selection;
using Microsoft.FSharp.Collections;

using Dynamo.Controls;
using Dynamo.Utilities;
using Dynamo.Connectors;
using Dynamo.FSchemeInterop.Node;
using Dynamo.FSchemeInterop;
using Dynamo.Commands;
using Microsoft.Practices.Prism.ViewModel;
using Value = Dynamo.FScheme.Value;


namespace Dynamo.Nodes
{
    public enum ElementState { DEAD, ACTIVE, ERROR };

    public enum LacingStrategy
    {
        Longest,
        Shortest,
        Single
    };

    public delegate void PortsChangedHandler(object sender, EventArgs e);

    public delegate void DispatchedToUIThreadHandler(object sender, UIDispatcherEventArgs e);

    public abstract class dynNodeModel : dynModelBase
    {
        /* TODO:
         * Incorporate INode in here somewhere
         */

        #region Abstract Members

        /// <summary>
        /// The dynElement's Evaluation Logic.
        /// </summary>
        /// <param name="args">Arguments to the node. You are guaranteed to have as many arguments as you have InPorts at the time it is run.</param>
        /// <returns>An expression that is the result of the Node's evaluation. It will be passed along to whatever the OutPort is connected to.</returns>
        public virtual void Evaluate(FSharpList<Value> args, Dictionary<PortData, Value> outPuts)
        {
            throw new NotImplementedException();
        }

        #endregion

        public event DispatchedToUIThreadHandler DispatchedToUI;
        public void OnDispatchedToUI(object sender, UIDispatcherEventArgs e)
        {
            if (DispatchedToUI != null)
                DispatchedToUI(this, e);
        }

        public dynWorkspaceModel WorkSpace;
        public ObservableCollection<PortData> InPortData { get; private set; }
        public ObservableCollection<PortData> OutPortData { get; private set; }
        Dictionary<dynPortModel, PortData> portDataDict = new Dictionary<dynPortModel, PortData>();
        
//MVVM : node should not reference its view directly
        //public dynNodeView NodeUI;
        
        public Dictionary<int, Tuple<int, dynNodeModel>> Inputs = 
            new Dictionary<int, Tuple<int, dynNodeModel>>();
        public Dictionary<int, HashSet<Tuple<int, dynNodeModel>>> Outputs =
            new Dictionary<int, HashSet<Tuple<int, dynNodeModel>>>();

        private Dictionary<int, Tuple<int, dynNodeModel>> previousInputPortMappings = 
            new Dictionary<int, Tuple<int, dynNodeModel>>();
        private Dictionary<int, HashSet<Tuple<int, dynNodeModel>>> previousOutputPortMappings =
            new Dictionary<int, HashSet<Tuple<int, dynNodeModel>>>();
        ObservableCollection<dynPortModel> inPorts = new ObservableCollection<dynPortModel>();
        ObservableCollection<dynPortModel> outPorts = new ObservableCollection<dynPortModel>();
        private LacingStrategy _argumentLacing  = LacingStrategy.Single;
        private string _nickName;
        ElementState state;
        string toolTipText = "";
        //bool isSelected = false;
        private bool _isCustomFunction = false;
        private bool interactionEnabled = true;

        /// <summary>
        /// Returns whether this node represents a built-in or custom function.
        /// </summary>
        public bool IsCustomFunction
        {
            get { return this.GetType().IsAssignableFrom(typeof(dynFunction)); }
        }

        public ElementState State
        {
            get { return state; }
            set
            {
                if (value != ElementState.ERROR)
                {
                    SetTooltip();
                }

                state = value;
                RaisePropertyChanged("State");
            }
        }

        public string ToolTipText
        {
            get
            {
                return toolTipText;
            }
            set
            {
                toolTipText = value;
                RaisePropertyChanged("ToolTipText");
            }
        }

        public string NickName
        {
            get { return _nickName; }
            set
            {
                _nickName = value;
                RaisePropertyChanged("NickName");
            }
        }

        public ObservableCollection<dynPortModel> InPorts
        {
            get { return inPorts; }
            set
            {
                inPorts = value;
                RaisePropertyChanged("InPorts");
            }
        }

        public ObservableCollection<dynPortModel> OutPorts
        {
            get { return outPorts; }
            set
            {
                outPorts = value;
                RaisePropertyChanged("OutPorts");
            }
        }

        /// <summary>
        /// Control how arguments lists of various sizes are laced.
        /// </summary>
        public LacingStrategy ArgumentLacing
        {
            get { return _argumentLacing; }
            set
            {
                _argumentLacing = value;
                isDirty = true;
                RaisePropertyChanged("LacingStrategy");
            }
        }

        /// <summary>
        ///     Category property
        /// </summary>
        /// <value>
        ///     If the node has a category, return it.  Other wise return empty string.
        /// </value>
        public string Category { 
            get
            {
                var type = GetType();
                object[] attribs = type.GetCustomAttributes(typeof(NodeCategoryAttribute), false);
                if (type.Namespace == "Dynamo.Nodes" &&
                    !type.IsAbstract &&
                    attribs.Length > 0 &&
                    type.IsSubclassOf(typeof (dynNodeModel)))
                {
                    NodeCategoryAttribute elCatAttrib = attribs[0] as NodeCategoryAttribute;
                    return elCatAttrib.ElementCategory;
                }                    
                return "";
            }
        }

        /// <summary>
        /// Should changes be reported to the containing workspace?
        /// </summary>
        private bool _report = true;

        /// <summary>
        /// Get the last computed value from the node.
        /// </summary>
        public Value OldValue { get; protected set; }


        protected internal ExecutionEnvironment macroEnvironment = null;

        //TODO: don't make this static (maybe)
        protected dynBench Bench
        {
            get { return dynSettings.Bench; }
        }

        protected DynamoController Controller
        {
            get { return dynSettings.Controller; }
        }

        /*public bool IsSelected
        {
            //TODO:Remove brush setting from here
            //brushes should be controlled by a converter

            get
            {
                return isSelected;
            }
            set
            {
                isSelected = value;
                RaisePropertyChanged("IsSelected");

                //MVVM : Set colors from a binding, not here.
                //if (isSelected)
                //{
                //    var inConnectors = inPorts.SelectMany(x => x.Connectors);
                //    var outConnectors = outPorts.SelectMany(x => x.Connectors);

                //    foreach (dynConnector c in inConnectors)
                //    {
                //        if (c.Start != null && c.Start.Owner.IsSelected)
                //        {
                //            c.StrokeBrush = new LinearGradientBrush(Colors.Cyan, Colors.Cyan, 0);
                //        }
                //        else
                //        {
                //            c.StrokeBrush = new LinearGradientBrush(Color.FromRgb(31, 31, 31), Colors.Cyan, 0);
                //        }
                //    }
                //    foreach (dynConnector c in outConnectors)
                //    {
                //        if (c.End != null & c.End.Owner.IsSelected)
                //        {
                //            c.StrokeBrush = new LinearGradientBrush(Colors.Cyan, Colors.Cyan, 0);
                //        }
                //        else
                //        {
                //            c.StrokeBrush = new LinearGradientBrush(Colors.Cyan, Color.FromRgb(31, 31, 31), 0);
                //        }
                //    }
                //}
                //else
                //{
                //    foreach (dynConnector c in inPorts.SelectMany(x => x.Connectors)
                //        .Concat(outPorts.SelectMany(x => x.Connectors)))
                //    {
                //        c.StrokeBrush = new SolidColorBrush(Color.FromRgb(31, 31, 31));
                //    }

                //}
            }
        }*/
        
        private bool _isDirty = true;

        ///<summary>
        ///Does this Element need to be regenerated? Setting this to true will trigger a modification event
        ///for the dynWorkspace containing it. If Automatic Running is enabled, setting this to true will
        ///trigger an evaluation.
        ///</summary>
        public virtual bool RequiresRecalc
        {
            get
            {
                //TODO: When marked as clean, remember so we don't have to re-traverse
                if (_isDirty)
                    return true;
                else
                {
                    bool dirty = Inputs.Values.Where(x => x != null).Any(x => x.Item2.RequiresRecalc);
                    _isDirty = dirty;

                    return dirty;
                }
            }
            set
            {
                _isDirty = value;
                if (value && _report && WorkSpace != null)
                    WorkSpace.Modified();
            }
        }

        /// <summary>
        /// Returns if this node requires a recalculation without checking input nodes.
        /// </summary>
        protected internal bool isDirty
        {
            get { return _isDirty; }
            set { RequiresRecalc = value; }
        }

        private bool _saveResult = false;
        /// <summary>
        /// Determines whether or not the output of this Element will be saved. If true, Evaluate() will not be called
        /// unless IsDirty is true. Otherwise, Evaluate will be called regardless of the IsDirty value.
        /// </summary>
        internal bool SaveResult
        {
            get
            {
                return _saveResult
                   && Enumerable.Range(0, InPortData.Count).All(HasInput);
            }
            set
            {
                _saveResult = value;
            }
        }

        /// <summary>
        /// Is this node an entry point to the program?
        /// </summary>
        public bool IsTopmost
        {
            get
            {
                return OutPorts == null
                    || OutPorts.All(x => !x.Connectors.Any());
            }
        }

        public List<string> Tags
        {
            get
            {
                Type t = GetType();
                object[] rtAttribs = t.GetCustomAttributes(typeof(NodeSearchTagsAttribute), true);

                if (rtAttribs.Length > 0)
                    return ((NodeSearchTagsAttribute)rtAttribs[0]).Tags;
                else
                    return new List<string>();

            }
        }

        public string Description
        {
            get
            {
                Type t = GetType();
                object[] rtAttribs = t.GetCustomAttributes(typeof(NodeDescriptionAttribute), true);
                return ((NodeDescriptionAttribute)rtAttribs[0]).ElementDescription;
            }
        }

        public bool InteractionEnabled
        {
            get { return interactionEnabled; }
            set 
            { 
                interactionEnabled = value;
                RaisePropertyChanged("InteractionEnabled");
            }
        }

        public dynNodeModel()
        {
            InPortData = new ObservableCollection<PortData>();
            OutPortData = new ObservableCollection<PortData>();
            //NodeUI = new dynNodeView(this);

            //Fetch the element name from the custom attribute.
            var nameArray = GetType().GetCustomAttributes(typeof(NodeNameAttribute), true);

            if (nameArray.Length > 0)
            {
                NodeNameAttribute elNameAttrib = nameArray[0] as NodeNameAttribute;
                if (elNameAttrib != null)
                {
                    NickName = elNameAttrib.Name;
                }
            }
            else
                NickName = "";

            this.IsSelected = false;

            State = ElementState.DEAD;
        }

        /// <summary>
        /// Check current ports against ports used for previous mappings.
        /// </summary>
        void CheckPortsForRecalc()
        {
            RequiresRecalc = Enumerable.Range(0, InPortData.Count).Any(
               delegate(int input)
               {
                   Tuple<int, dynNodeModel> oldInput;
                   Tuple<int, dynNodeModel> currentInput;

                   //this is dirty if there wasn't anything set last time (implying it was never run)...
                   return !previousInputPortMappings.TryGetValue(input, out oldInput)
                       || oldInput == null
                       || !TryGetInput(input, out currentInput)
                       //or If what's set doesn't match
                       || (oldInput.Item2 != currentInput.Item2 && oldInput.Item1 != currentInput.Item1);
               })
            || Enumerable.Range(0, OutPortData.Count).Any(
               delegate(int output)
               {
                   HashSet<Tuple<int, dynNodeModel>> oldOutputs;
                   HashSet<Tuple<int, dynNodeModel>> newOutputs;

                   return !previousOutputPortMappings.TryGetValue(output, out oldOutputs)
                       || !TryGetOutput(output, out newOutputs)
                       || oldOutputs.SetEquals(newOutputs);
               });
        }

        /// <summary>
        /// Override this to implement custom save data for your Element. If overridden, you should also override
        /// LoadElement() in order to read the data back when loaded.
        /// </summary>
        /// <param name="xmlDoc">The XmlDocument representing the whole workspace containing this Element.</param>
        /// <param name="dynEl">The XmlElement representing this Element.</param>
        public virtual void SaveElement(System.Xml.XmlDocument xmlDoc, System.Xml.XmlElement dynEl)
        {

        }

        /// <summary>
        /// Override this to implement loading of custom data for your Element. If overridden, you should also override
        /// SaveElement() in order to write the data when saved.
        /// </summary>
        /// <param name="elNode">The XmlNode representing this Element.</param>
        public virtual void LoadElement(System.Xml.XmlNode elNode)
        {

        }

        /// <summary>
        /// Forces the node to refresh it's dirty state by checking all inputs.
        /// </summary>
        public void MarkDirty()
        {
            bool dirty = false;
            foreach (var input in Inputs.Values.Where(x => x != null))
            {
                input.Item2.MarkDirty();
                if (input.Item2.RequiresRecalc)
                {
                    dirty = true;
                }
            }
            if (!_isDirty)
                _isDirty = dirty;
            return;
        }

        internal virtual INode BuildExpression(Dictionary<dynNodeModel, Dictionary<int, INode>> buildDict)
        {
            //Debug.WriteLine("Building expression...");

            if (OutPortData.Count > 1)
            {
                var names = OutPortData.Select(x => x.NickName).Zip(Enumerable.Range(0, OutPortData.Count), (x, i) => x+i);
                var listNode = new FunctionNode("list", names);
                foreach (var data in names.Zip(Enumerable.Range(0, OutPortData.Count), (name, index) => new { Name=name, Index=index }))
                {
                    listNode.ConnectInput(data.Name, Build(buildDict, data.Index));
                }
                return listNode;
            }
            else
                return Build(buildDict, 0);
        }

        //TODO: do all of this as the Ui is modified, simply return this?
        /// <summary>
        /// Builds an INode out of this Element. Override this or Compile() if you want complete control over this Element's
        /// execution.
        /// </summary>
        /// <returns>The INode representation of this Element.</returns>
        protected internal virtual INode Build(Dictionary<dynNodeModel, Dictionary<int, INode>> preBuilt, int outPort)
        {
            //Debug.WriteLine("Building node...");

            Dictionary<int, INode> result;
            if (preBuilt.TryGetValue(this, out result))
                return result[outPort];

            //Fetch the names of input ports.
            var portNames = InPortData.Zip(Enumerable.Range(0, InPortData.Count), (x, i) => x.NickName + i);

            //Compile the procedure for this node.
            InputNode node = Compile(portNames);

            //Is this a partial application?
            var partial = false;

            var partialSymList = new List<string>();

            //For each index in InPortData
            //for (int i = 0; i < InPortData.Count; i++)
            foreach (var data in Enumerable.Range(0, InPortData.Count).Zip(portNames, (data, name) => new { Index=data, Name=name }))
            {
                //Fetch the corresponding port
                //var port = InPorts[i];

                Tuple<int, dynNodeModel> input;

                //If this port has connectors...
                //if (port.Connectors.Any())
                if (TryGetInput(data.Index, out input))
                {
                    //Debug.WriteLine(string.Format("Connecting input {0}", data.Name));

                    //Compile input and connect it
                    node.ConnectInput(data.Name, input.Item2.Build(preBuilt, input.Item1));
                }
                else //othwise, remember that this is a partial application
                {
                    partial = true;
                    partialSymList.Add(data.Name);
                }
            }

            var nodes = new Dictionary<int, INode>();

            if (OutPortData.Count > 1)
            {
                foreach (var data in partialSymList)
                    node.ConnectInput(data, new SymbolNode(data));

                InputNode prev = node;
                int prevIndex = 0;

                foreach (var data in Enumerable.Range(0, OutPortData.Count).Zip(OutPortData, (i, d) => new { Index = i, Data = d }))
                {
                    if (HasOutput(data.Index))
                    {
                        if (data.Index > 0)
                        {
                            var diff = data.Index - prevIndex;
                            InputNode restNode;
                            if (diff > 1)
                            {
                                restNode = new ExternalFunctionNode(FScheme.Drop, new List<string>() { "amt", "list" });
                                restNode.ConnectInput("amt", new NumberNode(diff));
                                restNode.ConnectInput("list", prev);
                            }
                            else
                            {
                                restNode = new ExternalFunctionNode(FScheme.Cdr, new List<string>() { "list" });
                                restNode.ConnectInput("list", prev);
                            }
                            prev = restNode;
                            prevIndex = data.Index;
                        }

                        var firstNode = new ExternalFunctionNode(FScheme.Car, new List<string>() { "list" });
                        firstNode.ConnectInput("list", prev);

                        if (partial)
                            nodes[data.Index] = new AnonymousFunctionNode(partialSymList, firstNode);
                        else
                            nodes[data.Index] = firstNode;
                    }
                }
            }
            else
            {
                nodes[outPort] = node;
            }

            //If this is a partial application, then remember not to re-eval.
            if (partial)
            {
                RequiresRecalc = false;
            }
            
            preBuilt[this] = nodes;

            //And we're done
            return nodes[outPort];
        }

        /// <summary>
        /// Compiles this Element into a ProcedureCallNode. Override this instead of Build() if you don't want to set up all
        /// of the inputs for the ProcedureCallNode.
        /// </summary>
        /// <param name="portNames">The names of the inputs to the node.</param>
        /// <returns>A ProcedureCallNode which will then be processed recursively to be connected to its inputs.</returns>
        protected virtual InputNode Compile(IEnumerable<string> portNames)
        {
            //Debug.WriteLine(string.Format("Compiling InputNode with ports {0}.", string.Join(",", portNames)));

            //Return a Function that calls eval.
            return new ExternalFunctionNode(evalIfDirty, portNames);
        }

        /// <summary>
        /// Called right before Evaluate() is called. Useful for processing side-effects without touching Evaluate()
        /// </summary>
        protected virtual void OnEvaluate() { }

        /// <summary>
        /// Called when the node's workspace has been saved.
        /// </summary>
        protected internal virtual void OnSave() { }

        internal void onSave()
        {
            savePortMappings();
            OnSave();
        }

        private void savePortMappings()
        {
            //Save all of the connection states, so we can check if this is dirty
            foreach (var data in Enumerable.Range(0, InPortData.Count))
            {
                Tuple<int, dynNodeModel> input;

                previousInputPortMappings[data] = TryGetInput(data, out input)
                   ? input
                   : null;
            }

            foreach (var data in Enumerable.Range(0, OutPortData.Count))
            {
                HashSet<Tuple<int, dynNodeModel>> outputs;

                previousOutputPortMappings[data] = TryGetOutput(data, out outputs)
                    ? outputs
                    : new HashSet<Tuple<int, dynNodeModel>>();
            }
        }

        private Value evalIfDirty(FSharpList<Value> args)
        {
            if (OldValue == null || !SaveResult || RequiresRecalc)
            {
                //Evaluate arguments, then evaluate 
                OldValue = evaluateNode(args);
            }
            else
                OnEvaluate();

            return OldValue;
        }

        private delegate Value innerEvaluationDelegate();

        public Dictionary<PortData, Value> evaluationDict = new Dictionary<PortData, Value>();

        public Value GetValue(int outPortIndex)
        {
            return evaluationDict.Values.ElementAt(outPortIndex);
        }

        protected internal virtual Value evaluateNode(FSharpList<Value> args)
        {
            //Debug.WriteLine("Evaluating node...");

            if (SaveResult)
            {
                savePortMappings();
            }

            evaluationDict.Clear();

            object[] iaAttribs = GetType().GetCustomAttributes(typeof(IsInteractiveAttribute), false);
            bool isInteractive = iaAttribs.Length > 0 && ((IsInteractiveAttribute)iaAttribs[0]).IsInteractive;

            innerEvaluationDelegate evaluation = delegate
            {
                Value expr = null;

                if (Controller.RunCancelled)
                    throw new CancelEvaluationException(false);

                try
                {
                    __eval_internal(args, evaluationDict);

                    expr = OutPortData.Count == 1
                        ? evaluationDict[OutPortData[0]]
                        : Value.NewList(
                            Utils.SequenceToFSharpList(
                                evaluationDict.OrderBy(
                                    pair => OutPortData.IndexOf(pair.Key))
                                .Select(
                                    pair => pair.Value)));

//MVVM : don't use the dispatcher to invoke here
                    //NodeUI.Dispatcher.BeginInvoke(new Action(
                    //    delegate
                    //    {
                    //        NodeUI.UpdateLayout();
                    //        NodeUI.ValidateConnections();
                    //    }
                    //));
                }
                catch (CancelEvaluationException ex)
                {
                    OnRunCancelled();
                    throw ex;
                }
                catch (Exception ex)
                {
                    Bench.Dispatcher.Invoke(new Action(
                       delegate
                       {
                           Debug.WriteLine(ex.Message + " : " + ex.StackTrace);
                           dynSettings.Controller.DynamoViewModel.Log(ex);

                           if (DynamoCommands.WriteToLogCmd.CanExecute(null))
                           {
                               DynamoCommands.WriteToLogCmd.Execute(ex.Message);
                               DynamoCommands.WriteToLogCmd.Execute(ex.StackTrace);
                           }

                           Controller.DynamoViewModel.ShowElement(this);
                       }
                    ));

                    Error(ex.Message);
                }

                OnEvaluate();

                RequiresRecalc = false;

                return expr;
            };

//MVVM : Switched from nodeUI dispatcher to bench dispatcher 
            Value result = isInteractive && dynSettings.Bench != null
                ? (Value)dynSettings.Bench.Dispatcher.Invoke(evaluation)
                : evaluation();

            if (result != null)
                return result;
            else
                throw new Exception("");
        }

        protected virtual void OnRunCancelled()
        {

        }
        
        protected internal virtual void __eval_internal(FSharpList<Value> args, Dictionary<PortData, Value> outPuts)
        {
            var argList = new List<string>();
            if (args.Any())
            {
                argList = args.Select(x => x.ToString()).ToList<string>();
            }
            var outPutsList = new List<string>();
            if(outPuts.Any())
            {
                outPutsList = outPuts.Keys.Select(x=>x.NickName).ToList<string>();
            }

            Debug.WriteLine(string.Format("__eval_internal : {0} : {1}", 
                string.Join(",", argList), 
                string.Join(",", outPutsList)));

            Evaluate(args, outPuts);
        }
        
        /// <summary>
        /// Destroy this dynElement
        /// </summary>
        public virtual void Destroy() { }

        protected internal void DisableReporting()
        {
            _report = false;
        }

        protected internal void EnableReporting()
        {
            _report = true;
        }

        protected internal bool ReportingEnabled { get { return _report; } }

        /// <summary>
        /// Creates a Scheme representation of this dynNode and all connected dynNodes.
        /// </summary>
        /// <returns>S-Expression</returns>
        public virtual string PrintExpression()
        {
            var nick = NickName.Replace(' ', '_');

            if (!Enumerable.Range(0, InPortData.Count).Any(HasInput))
                return nick;

            string s = "";

            if (Enumerable.Range(0, InPortData.Count).All(HasInput))
            {
                s += "(" + nick;
                //for (int i = 0; i < InPortData.Count; i++)
                foreach (int data in Enumerable.Range(0, InPortData.Count))
                {
                    Tuple<int, dynNodeModel> input;
                    TryGetInput(data, out input);
                    s += " " + input.Item2.PrintExpression();
                }
                s += ")";
            }
            else
            {
                s += "(lambda ("
                   + string.Join(" ", InPortData.Where((_, i) => !HasInput(i)).Select(x => x.NickName))
                   + ") (" + nick;
                //for (int i = 0; i < InPortData.Count; i++)
                foreach (int data in Enumerable.Range(0, InPortData.Count))
                {
                    s += " ";
                    Tuple<int, dynNodeModel> input;
                    if (TryGetInput(data, out input))
                        s += input.Item2.PrintExpression();
                    else
                        s += InPortData[data].NickName;
                }
                s += "))";
            }

            return s;
        }

        internal void ConnectInput(int inputData, int outputData, dynNodeModel node)
        {
            Inputs[inputData] = Tuple.Create(outputData, node);
            CheckPortsForRecalc();
        }
        
        internal void ConnectOutput(int portData, int inputData, dynNodeModel nodeLogic)
        {
            if (!Outputs.ContainsKey(portData))
                Outputs[portData] = new HashSet<Tuple<int, dynNodeModel>>();
            Outputs[portData].Add(Tuple.Create(inputData, nodeLogic));
        }

        internal void DisconnectInput(int data)
        {
            Inputs[data] = null;
            CheckPortsForRecalc();
        }

        /// <summary>
        /// Attempts to get the input for a certain port.
        /// </summary>
        /// <param name="data">PortData to look for an input for.</param>
        /// <param name="input">If an input is found, it will be assigned.</param>
        /// <returns>True if there is an input, false otherwise.</returns>
        public bool TryGetInput(int data, out Tuple<int, dynNodeModel> input)
        {
            return Inputs.TryGetValue(data, out input) && input != null;
        }

        public bool TryGetOutput(int output, out HashSet<Tuple<int, dynNodeModel>> newOutputs)
        {
            return Outputs.TryGetValue(output, out newOutputs);
        }

        /// <summary>
        /// Checks if there is an input for a certain port.
        /// </summary>
        /// <param name="data">PortData to look for an input for.</param>
        /// <returns>True if there is an input, false otherwise.</returns>
        public bool HasInput(int data)
        {
            return Inputs.ContainsKey(data) && Inputs[data] != null;
        }

        public bool HasOutput(int portData)
        {
            return Outputs.ContainsKey(portData) && Outputs[portData].Any();
        }

        internal void DisconnectOutput(int portData, int inPortData)
        {
            HashSet<Tuple<int, dynNodeModel>> output;
            if (Outputs.TryGetValue(portData, out output))
                output.RemoveWhere(x => x.Item1 == inPortData);
            CheckPortsForRecalc();
        }

        /// <summary>
        /// Implement on derived classes to cleanup resources when 
        /// </summary>
        public virtual void Cleanup()
        {
        }

        public void RegisterAllPorts()
        {
            RegisterInputs();
            RegisterOutputs();

            //UpdateLayout();

            ValidateConnections();
        }

        /// <summary>
        /// Add a port to this node
        /// </summary>
        /// <param name="portType"></param>
        /// <param name="name"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public dynPortModel AddPort(PortType portType, string name, int index)
        {
            if (portType == PortType.INPUT)
            {
                if (inPorts.Count > index)
                {
                    return inPorts[index];
                }
                else
                {
                    dynPortModel p = new dynPortModel(index, portType, this, name);

                    InPorts.Add(p);

                    //register listeners on the port
                    p.PortConnected += new PortConnectedHandler(p_PortConnected);
                    p.PortDisconnected += new PortConnectedHandler(p_PortDisconnected);

                    return p;
                }
            }
            else if (portType == PortType.OUTPUT)
            {
                if (outPorts.Count > index)
                {
                    return outPorts[index];
                }
                else
                {
                    dynPortModel p = new dynPortModel(index, portType, this, name);

                    OutPorts.Add(p);

                    //register listeners on the port
                    p.PortConnected += new PortConnectedHandler(p_PortConnected);
                    p.PortDisconnected += new PortConnectedHandler(p_PortDisconnected);

                    return p;
                }
            }
            return null;
        }

        //TODO: call connect and disconnect for dynNode

        /// <summary>
        /// When a port is connected, register a listener for the dynElementUpdated event
        /// and tell the object to build
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void p_PortConnected(object sender, EventArgs e)
        {
            ValidateConnections();

            var port = (dynPortModel)sender;
            if (port.PortType == PortType.INPUT)
            {
                var data = InPorts.IndexOf(port);
                var startPort = port.Connectors[0].Start;
                var outData = startPort.Owner.OutPorts.IndexOf(startPort);
                ConnectInput(
                    data,
                    outData,
                    startPort.Owner);
                startPort.Owner.ConnectOutput(
                    outData,
                    data,
                    this
                );
            }
        }

        void p_PortDisconnected(object sender, EventArgs e)
        {
            var port = (dynPortModel)sender;
            if (port.PortType == PortType.INPUT)
            {
                var data = InPorts.IndexOf(port);
                var startPort = port.Connectors[0].Start;
                DisconnectInput(data);
                startPort.Owner.DisconnectOutput(
                    startPort.Owner.OutPorts.IndexOf(startPort),
                    data);
            }
        }

        private void RemovePort(dynPortModel inport)
        {
            if (inport.PortType == PortType.INPUT)
            {
                //int index = inPorts.FindIndex(x => x == inport);
                int index = inPorts.IndexOf(inport);
                //gridLeft.Children.Remove(inport);

                while (inport.Connectors.Any())
                {
                    inport.Connectors[0].Kill();
                }
            }
        }

        /// <summary>
        /// Reads inputs list and adds ports for each input.
        /// </summary>
        public void RegisterInputs()
        {
            //read the inputs list and create a number of
            //input ports
            int count = 0;
            foreach (PortData pd in InPortData)
            {
                //add a port for each input
                //distribute the ports along the 
                //edges of the icon
                var port = AddPort(PortType.INPUT, InPortData[count].NickName, count);

                //MVVM: AddPort now returns a port model. You can't set the data context here.
                //port.DataContext = this;

                portDataDict[port] = pd;
                count++;
            }

            if (inPorts.Count > count)
            {
                foreach (var inport in inPorts.Skip(count))
                {
                    RemovePort(inport);
                }

                for (int i = inPorts.Count - 1; i >= count; i--)
                {
                    inPorts.RemoveAt(i);
                }
                //InPorts.RemoveRange(count, inPorts.Count - count);
            }
        }

        /// <summary>
        /// Reads outputs list and adds ports for each output
        /// </summary>
        public void RegisterOutputs()
        {
            //read the inputs list and create a number of
            //input ports
            int count = 0;
            foreach (PortData pd in OutPortData)
            {
                //add a port for each input
                //distribute the ports along the 
                //edges of the icon
                var port = AddPort(PortType.OUTPUT, pd.NickName, count);

//MVVM : don't set the data context in the model
                //port.DataContext = this;

                portDataDict[port] = pd;
                count++;
            }

            if (outPorts.Count > count)
            {
                foreach (var outport in outPorts.Skip(count))
                {
                    RemovePort(outport);
                }

                for (int i = outPorts.Count - 1; i >= count; i--)
                {
                    outPorts.RemoveAt(i);
                }

                //OutPorts.RemoveRange(count, outPorts.Count - count);
            }
        }

        void SetTooltip()
        {
            Type t = GetType();
            object[] rtAttribs = t.GetCustomAttributes(typeof(NodeDescriptionAttribute), true);
            if (rtAttribs.Length > 0)
            {
                string description = ((NodeDescriptionAttribute)rtAttribs[0]).ElementDescription;
                ToolTipText = description;
            }
        }

        /// <summary>
        /// Color the connection according to it's port connectivity
        /// if all ports are connected, color green, else color orange
        /// </summary>
        public void ValidateConnections()
        {
            // if there are inputs without connections
            // mark as dead
            State = inPorts.Select(x => x).Any(x => x.Connectors.Count == 0) ? ElementState.DEAD : ElementState.ACTIVE;
        }

        private void SetState(dynNodeView el, ElementState state)
        {
            State = state;
        }

        public void Error(string p)
        {
            State = ElementState.ERROR;
            ToolTipText = p;
        }

        public void SelectNeighbors()
        {
            var outConnectors = this.outPorts.SelectMany(x => x.Connectors);
            var inConnectors = this.inPorts.SelectMany(x => x.Connectors);

            foreach (dynConnectorModel c in outConnectors)
            {
                if (!DynamoSelection.Instance.Selection.Contains(c.End.Owner))
                    DynamoSelection.Instance.Selection.Add(c.End.Owner);
            }

            foreach (dynConnectorModel c in inConnectors)
            {
                if (!DynamoSelection.Instance.Selection.Contains(c.Start.Owner))
                    DynamoSelection.Instance.Selection.Add(c.Start.Owner);
            }
        }

        //private Dictionary<UIElement, bool> enabledDict
        //    = new Dictionary<UIElement, bool>();

        internal void DisableInteraction()
        {
//MVVM : IsEnabled on input grid elements is now bount to InteractionEnabled property
            //enabledDict.Clear();

            //foreach (UIElement e in inputGrid.Children)
            //{
            //    enabledDict[e] = e.IsEnabled;

            //    e.IsEnabled = false;
            //}
            State = ElementState.DEAD;
            InteractionEnabled = false;
        }

        internal void EnableInteraction()
        {
            //foreach (UIElement e in inputGrid.Children)
            //{
            //    if (enabledDict.ContainsKey(e))
            //        e.IsEnabled = enabledDict[e];
            //}
            ValidateConnections();
            InteractionEnabled = true;
        }

        /// <summary>
        /// Called back from the view to enable users to setup their own view elements
        /// </summary>
        /// <param name="parameter"></param>
        public virtual void SetupCustomUIElements(dynNodeView NodeUI)
        {
            
        }

        /// <summary>
        /// Called by nodes for behavior that they want to dispatch on the UI thread
        /// Triggers event to be received by the UI. If no UI exists, behavior will not be executed.
        /// </summary>
        /// <param name="a"></param>
        public void DispatchOnUIThread(Action a)
        {
            OnDispatchedToUI(this, new UIDispatcherEventArgs(a));
        }

        #region ISelectable Interface

        public override void Deselect()
        {
            ValidateConnections();
            IsSelected = false;
        }
        #endregion
    }

    public abstract class dynNodeWithOneOutput : dynNodeModel
    {
        public override void Evaluate(FSharpList<Value> args, Dictionary<PortData, Value> outPuts)
        {
            outPuts[OutPortData[0]] = Evaluate(args);
        }

        public virtual Value Evaluate(FSharpList<Value> args)
        {
            throw new NotImplementedException();
        }
    }

    #region class attributes
    [AttributeUsage(AttributeTargets.All)]
    public class NodeNameAttribute : System.Attribute
    {
        public string Name { get; set; }

        public NodeNameAttribute(string elementName)
        {
            Name = elementName;
        }
    }

    [AttributeUsage(AttributeTargets.All)]
    public class NodeCategoryAttribute : System.Attribute
    {
        public string ElementCategory { get; set; }

        public NodeCategoryAttribute(string category)
        {
            ElementCategory = category;
        }
    }

    [AttributeUsage(AttributeTargets.All)]
    public class NodeSearchTagsAttribute : System.Attribute
    {
        public List<string> Tags { get; set; }

        public NodeSearchTagsAttribute(params string[] tags)
        {
            Tags = tags.ToList();
        }
    }

    [AttributeUsage(AttributeTargets.All, Inherited = true)]
    public class IsInteractiveAttribute : System.Attribute
    {
        public bool IsInteractive { get; set; }

        public IsInteractiveAttribute(bool isInteractive)
        {
            IsInteractive = isInteractive;
        }
    }

    [AttributeUsage(AttributeTargets.All)]
    public class NodeDescriptionAttribute : System.Attribute
    {
        public string ElementDescription
        {
            get;
            set;
        }

        public NodeDescriptionAttribute(string description)
        {
            ElementDescription = description;
        }
    }
    #endregion

    public class PredicateTraverser
    {
        Predicate<dynNodeModel> predicate;

        Dictionary<dynNodeModel, bool> resultDict = new Dictionary<dynNodeModel, bool>();

        bool inProgress;

        public PredicateTraverser(Predicate<dynNodeModel> p)
        {
            predicate = p;
        }

        public bool TraverseUntilAny(dynNodeModel entry)
        {
            inProgress = true;
            bool result = traverseAny(entry);
            resultDict.Clear();
            inProgress = false;
            return result;
        }

        public bool ContinueTraversalUntilAny(dynNodeModel entry)
        {
            if (inProgress)
                return traverseAny(entry);
            else
                throw new Exception("ContinueTraversalUntilAny cannot be used except in a traversal predicate.");
        }

        private bool traverseAny(dynNodeModel entry)
        {
            bool result;
            if (resultDict.TryGetValue(entry, out result))
                return result;

            result = predicate(entry);
            resultDict[entry] = result;
            if (result)
                return result;

            if (entry is dynFunction)
            {
                var symbol = Guid.Parse((entry as dynFunction).Symbol);
                if (!dynSettings.Controller.CustomNodeLoader.Contains(symbol))
                {
                    dynSettings.Controller.DynamoViewModel.Log("WARNING -- No implementation found for node: " + symbol);
                    entry.Error("Could not find .dyf definition file for this node.");
                    return false;
                }

                result = dynSettings.Controller.CustomNodeLoader.GetFunctionDefinition(symbol)
                    .Workspace.GetTopMostNodes().Any(ContinueTraversalUntilAny);
            }
            resultDict[entry] = result;
            if (result)
                return result;

            return entry.Inputs.Values.Any(x => x != null && traverseAny(x.Item2));
        }
    }

    public class UIDispatcherEventArgs:EventArgs
    {
        public Action ActionToDispatch { get; set; }
        public UIDispatcherEventArgs(Action a)
        {
            ActionToDispatch = a;
        }
    }
}