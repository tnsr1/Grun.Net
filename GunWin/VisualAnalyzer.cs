﻿#region BSD 3-Clause License

// <copyright file="VisualAnalyzer.cs" company="Edgerunner.org">
// Copyright 2020 Thaddeus Ryker
// </copyright>
// 
// BSD 3-Clause License
// 
// Copyright (c) 2020, Thaddeus Ryker
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 
// 1. Redistributions of source code must retain the above copyright notice, this
//    list of conditions and the following disclaimer.
// 
// 2. Redistributions in binary form must reproduce the above copyright notice,
//    this list of conditions and the following disclaimer in the documentation
//    and/or other materials provided with the distribution.
// 
// 3. Neither the name of the copyright holder nor the names of its
//    contributors may be used to endorse or promote products derived from
//    this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

using BrightIdeasSoftware;

using FastColoredTextBoxNS;

using JetBrains.Annotations;

using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.GraphViewerGdi;
using Microsoft.Msagl.Layout.Layered;

using Org.Edgerunner.ANTLR4.Tools.Graphing;
using Org.Edgerunner.ANTLR4.Tools.Testing.Grammar;
using Org.Edgerunner.ANTLR4.Tools.Testing.Grammar.Errors;
using Org.Edgerunner.ANTLR4.Tools.Testing.GrunWin.Properties;

namespace Org.Edgerunner.ANTLR4.Tools.Testing.GrunWin
{
   /// <summary>
   ///    Class that represents an ANTLR grammar test Analyzer.
   ///    Implements the <see cref="System.Windows.Forms.Form" />
   /// </summary>
   /// <seealso cref="System.Windows.Forms.Form" />
   public partial class VisualAnalyzer : Form
   {
      private string _DefaultRule;

      private GrammarReference _Grammar;

      private List<string> _ParserRules;

      private GViewer _Viewer;

      #region Constructors And Finalizers

      /// <summary>
      ///    Initializes a new instance of the <see cref="VisualAnalyzer" /> class.
      /// </summary>
      public VisualAnalyzer()
      {
         InitializeComponent();
      }

      #endregion

      /// <summary>
      ///    Gets or sets a value indicating whether to parse with diagnostics enabled.
      /// </summary>
      /// <value><c>true</c> if diagnostic parsing is enabled; otherwise, <c>false</c>.</value>
      /// <remarks>If SLL is also enabled, SLL will supersede diagnostic mode.</remarks>
      public bool ParseWithDiagnostics
      {
         get => diagnosticsToolStripMenuItem.Checked;
         set
         {
            diagnosticsToolStripMenuItem.Checked = value;
            ParseSource();
         }
      }

      /// <summary>
      ///    Gets or sets a value indicating whether to parse with Simple LL mode.
      /// </summary>
      /// <value><c>true</c> if SLL parsing mode is enabled; otherwise, <c>false</c>.</value>
      /// <remarks>If Diagnostics are also enabled, SLL will supersede diagnostic mode.</remarks>
      public bool ParseWithSllMode
      {
         get => simpleLLModeToolStripMenuItem.Checked;
         set
         {
            simpleLLModeToolStripMenuItem.Checked = value;
            ParseSource();
         }
      }

      /// <summary>
      ///    Gets or sets a value indicating whether to parse with tracing enabled.
      /// </summary>
      /// <value><c>true</c> if tracing is enabled; otherwise, <c>false</c>.</value>
      public bool ParseWithTracing
      {
         get => tracingToolStripMenuItem.Checked;
         set
         {
            tracingToolStripMenuItem.Checked = value;
            ParseSource();
         }
      }

      /// <summary>
      ///    Parses the source code.
      /// </summary>
      /// <exception cref="T:Org.Edgerunner.ANTLR4.Tools.Testing.Exceptions.GrammarException">
      /// No parser found for the current grammar
      /// OR
      /// Selected parser rule does not exist for the current grammar.
      /// </exception>
      /// <exception cref="T:System.ArgumentNullException">Selected parser rule is null or empty.</exception>
      public void ParseSource()
      {
         if (_Grammar == null)
            return;

         if (_ParserRules.Count == 0)
            return;

         if (string.IsNullOrEmpty(CmbRules.SelectedItem?.ToString()))
            return;

         var listener = new TestingErrorListener();
         var analyzer = new Analyzer(_Grammar, CodeEditor.Text);
         var options = ParseOption.Tree;
         if (ParseWithDiagnostics) options |= ParseOption.Diagnostics;
         if (ParseWithSllMode) options |= ParseOption.Sll;
         if (ParseWithTracing) options |= ParseOption.Trace;
         analyzer.Parse(CmbRules.SelectedItem.ToString(), options, listener);
         PopulateTokens(analyzer.DisplayTokens);
         PopulateParserMessages(listener.Errors);
         BuildParseTreeTreeViewGuide(analyzer.ParseContext);
         BuildParseTreeGraph(analyzer.ParseContext);
      }

      /// <summary>
      ///    Sets the default parser rule.
      /// </summary>
      /// <param name="rule">The parser rule.</param>
      /// <exception cref="T:System.ArgumentException">Invalid parser rule.</exception>
      /// <exception cref="T:System.ArgumentNullException"><paramref name="rule" /> is <see langword="null" /></exception>
      public void SetDefaultParserRule(string rule)
      {
         if (string.IsNullOrEmpty(rule)) throw new ArgumentNullException(nameof(rule));

         if (!_ParserRules.Contains(rule))
            throw new ArgumentException(string.Format(Resources.InvalidParserRule, rule), nameof(rule));

         _DefaultRule = rule;
         CmbRules.SelectedIndex = CmbRules.FindStringExact(rule);
      }

      /// <summary>
      ///    Sets the grammar to parse.
      /// </summary>
      /// <param name="grammar">The grammar.</param>
      public void SetGrammar(GrammarReference grammar)
      {
         var scanner = new Scanner();
         _Grammar = grammar;
         _ParserRules = scanner.GetParserRulesForGrammar(grammar).ToList();
         LoadParserRules();
         stripLabelGrammarName.Text = grammar.GrammarName;
      }

      /// <summary>
      ///    Sets the source code to be analyzed.
      /// </summary>
      /// <param name="code">The code.</param>
      public void SetSourceCode(string code)
      {
         CodeEditor.Text = code;
      }

      private void _Viewer_Click(object sender, EventArgs e)
      {
         if (_Viewer.SelectedObject is Node node)
            ShowSourceForTreeNode(node.UserData as ITree ?? throw new InvalidOperationException());
      }

      private void AddTreeBranchesAndLeaves(TreeNode treeNode, ITree tree)
      {
         for (var i = 0; i < tree.ChildCount; i++)
         {
            var child = tree.GetChild(i);
            var newNode =
               new TreeNode(Trees.GetNodeText(child, _ParserRules))
                  {
                     Tag = child, Name = child.GetHashCode().ToString()
                  };
            treeNode.Nodes.Add(newNode);
            AddTreeBranchesAndLeaves(newNode, child);
         }
      }

      private void BuildParseTreeGraph(ITree tree, int? zoomFactor = null)
      {
         if (_Viewer == null)
            return;

         if (tree == null)
            return;

         var grapher = new ParseTreeGrapher(tree, _ParserRules)
                          {
                             BackgroundColor = Color.LightBlue, BorderColor = Color.Black, TextColor = Color.Black
                          };
         var graph = grapher.CreateGraph();
         graph.LayoutAlgorithmSettings = new SugiyamaLayoutSettings();
         _Viewer.SuspendLayout();
         _Viewer.Graph = graph;
         if (zoomFactor.HasValue)
            GraphZoomTrackBar.Value = zoomFactor.Value;
         _Viewer.ZoomF = GraphZoomTrackBar.Value;
         _Viewer.ResumeLayout();
      }

      private void BuildParseTreeTreeViewGuide(ITree tree)
      {
         ParseTreeView.SuspendLayout();
         ParseTreeView.Nodes.Clear();

         if (tree == null)
         {
            ParseTreeView.ResumeLayout();
            return;
         }

         var treeNode =
            new TreeNode(Trees.GetNodeText(tree, _ParserRules)) { Tag = tree, Name = tree.GetHashCode().ToString() };
         ParseTreeView.Nodes.Add(treeNode);
         AddTreeBranchesAndLeaves(treeNode, tree);

         ParseTreeView.ResumeLayout();
      }

      private void ParserRulesCombo_SelectedIndexChanged(object sender, EventArgs e)
      {
         if (CmbRules.Items.Count > 0)
            ParseSource();
      }

      private void CodeEditor_TextChanged(object sender, TextChangedEventArgs e)
      {
         ParseSource();
      }

      private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
      {
         Application.Exit();
      }

      private void GraphZoomTrackBar_ValueChanged(object sender, EventArgs e)
      {
         if (_Viewer != null)
            _Viewer.ZoomF = GraphZoomTrackBar.Value;
      }

      private void InitializeGraphCanvas()
      {
         SuspendLayout();
         _Viewer = new GViewer();
         PnlGraph.Controls.Add(_Viewer);
         _Viewer.Dock = DockStyle.Fill;
         ResumeLayout();
         _Viewer.LayoutAlgorithmSettingsButtonVisible = false;
         _Viewer.Click += _Viewer_Click;
         var viewerContextMenu = new ContextMenu();
         var menuItem = viewerContextMenu.MenuItems.Add("Graph from here");
         menuItem.Click += Menu_GraphFromHere_Click;
         _Viewer.ContextMenu = viewerContextMenu;
      }

      private void LoadGrammarToolStripMenuItem_Click(object sender, EventArgs e)
      {
         openFileDialog.InitialDirectory = Environment.CurrentDirectory;
         openFileDialog.Filter = Resources.AssemblyFileFilter;
         openFileDialog.DefaultExt = "dll";

         if (openFileDialog.ShowDialog() == DialogResult.Cancel)
            return;

         var fileToSearch = openFileDialog.FileName;
         var scanner = new Scanner();
         var grammars = scanner.LocateAllGrammarsInFile(fileToSearch);
         var selectableGrammars = grammars as GrammarReference[] ?? grammars.ToArray();
         var grammarCount = selectableGrammars.Count();
         GrammarReference grammarToLoad = null;

         if (grammarCount == 0)
         {
            // TODO: throw error message to user about no grammars found
         }
         else if (grammarCount == 1)
         {
            grammarToLoad = selectableGrammars.First();
         }
         else
         {
            var selector = new GrammarSelector { GrammarsToSelectFrom = selectableGrammars };
            if (selector.ShowDialog() == DialogResult.Cancel)
               return;

            grammarToLoad = selector.SelectedGrammar;
         }

         SetGrammar(grammarToLoad);
      }

      private void LoadParserRules()
      {
         CmbRules.DataSource = _ParserRules.OrderBy(x => x).Distinct().ToList();
         CmbRules.Refresh();
      }

      private void LoadSourceToolStripMenuItem_Click(object sender, EventArgs e)
      {
         openFileDialog.InitialDirectory = Environment.CurrentDirectory;
         openFileDialog.Filter = Resources.AllFilesFilter;

         if (openFileDialog.ShowDialog() == DialogResult.Cancel)
            return;

         var fileToLoad = openFileDialog.FileName;
         using (var reader = new StreamReader(fileToLoad))
         {
            SetSourceCode(reader.ReadToEnd());
         }
      }

      private void Menu_GraphFromHere_Click(object sender, EventArgs e)
      {
         if (_Viewer.SelectedObject is Node node)
         {
            var treeNodes = ParseTreeView.Nodes.Find(node.UserData?.GetHashCode().ToString(), true);
            if (treeNodes.Length != 0)
            {
               var workingNode = treeNodes.First();
               ParseTreeView.SelectedNode = workingNode;
               BuildParseTreeGraph(workingNode.Tag as ITree, 1);
               ParseTreeView.Focus();
            }
         }
      }

      private void ParseMessageListView_Click(object sender, EventArgs e)
      {
         OLVListItem selected;
         if ((selected = ParseMessageListView.SelectedItem) != null)
            if (selected.RowObject != null)
            {
               var message = (ParseMessage)selected.RowObject;
               ShowSourcePosition(message.Token);
            }
      }

      private void ParseTreeView_AfterSelect(object sender, TreeViewEventArgs e)
      {
         // Now we graph and display just the selected branch
         if (e.Node.Tag is ITree selected)
         {
            BuildParseTreeGraph(selected);
            ShowSourceForTreeNode(selected);
         }
      }

      private void PopulateParserMessages(List<ParseMessage> listenerErrors)
      {
         ParseMessageListView.SetObjects(listenerErrors);
      }

      private void PopulateTokens(IList<TokenViewModel> tokens)
      {
         tokenListView.SetObjects(tokens);
      }

      private void ShowSourceForTreeNode([NotNull] ITree tree)
      {
         if (tree is null)
            throw new ArgumentNullException(nameof(tree));

         if (tree is ParserRuleContext context)
            ShowSourcePosition(context);
         else if (tree is ErrorNodeImpl errorTerminal)
            ShowSourcePosition(errorTerminal);
         else if (tree is TerminalNodeImpl terminal)
            ShowSourcePosition(terminal);
      }

      // ReSharper disable once FlagArgument
      private void ShowSourcePosition(ParserRuleContext context, bool setFocus = true)
      {
         if (context == null)
            return;

         var startingPlace = new Place(context.Start.Column, context.start.Line - 1);
         var stoppingPlace = new Place(context.Stop.Column + context.stop.Text.Length, context.stop.Line - 1);

         CodeEditor.Selection = new Range(CodeEditor, startingPlace, stoppingPlace);
         CodeEditor.DoCaretVisible();
         if (setFocus)
            CodeEditor.Focus();
      }

      // ReSharper disable once FlagArgument
      private void ShowSourcePosition(TerminalNodeImpl node, bool setFocus = true)
      {
         if (node == null)
            return;

         var startingPlace = new Place(node.Symbol.Column, node.Symbol.Line - 1);
         var stoppingPlace = new Place(node.Symbol.Column + node.Symbol.Text.Length, node.Symbol.Line - 1);

         CodeEditor.Selection = new Range(CodeEditor, startingPlace, stoppingPlace);
         CodeEditor.DoCaretVisible();
         if (setFocus)
            CodeEditor.Focus();
      }

      // ReSharper disable once FlagArgument
      private void ShowSourcePosition(ErrorNodeImpl node, bool setFocus = true)
      {
         if (node == null)
            return;

         var startingPlace = new Place(node.Symbol.Column, node.Symbol.Line - 1);
         var stoppingPlace = new Place(node.Symbol.Column + node.symbol.Text.Length, node.Symbol.Line - 1);

         CodeEditor.Selection = new Range(CodeEditor, startingPlace, stoppingPlace);
         CodeEditor.DoCaretVisible();
         if (setFocus)
            CodeEditor.Focus();
      }

      // ReSharper disable once FlagArgument
      private void ShowSourcePosition(IToken token, bool setFocus = true)
      {
         if (token == null)
            return;

         var startingPlace = new Place(token.Column, token.Line - 1);
         var stoppingPlace = new Place(token.Column + token.Text.Length, token.Line - 1);

         CodeEditor.Selection = new Range(CodeEditor, startingPlace, stoppingPlace);
         CodeEditor.DoCaretVisible();
         if (setFocus)
            CodeEditor.Focus();
      }

      private void TokenListView_Click(object sender, EventArgs e)
      {
         OLVListItem selected;
         if ((selected = tokenListView.SelectedItem) != null)
            if (selected.RowObject != null)
            {
               var tokenView = (TokenViewModel)selected.RowObject;
               ShowSourcePosition(tokenView.ActualToken, false);
            }
      }

      private void VisualAnalyzer_Load(object sender, EventArgs e)
      {
         InitializeGraphCanvas();

         if (!string.IsNullOrEmpty(CodeEditor.Text))
            ParseSource();
      }
   }
}