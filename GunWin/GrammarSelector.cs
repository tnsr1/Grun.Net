﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Org.Edgerunner.ANTLR4.Tools.Testing.GrunWin
{
   public partial class GrammarSelector : Form
   {
      private IEnumerable<GrammarReference> _GrammarsToSelectFrom;

      public GrammarSelector()
      {
         InitializeComponent();
      }

      /// <summary>
      /// Gets the selected grammar.
      /// </summary>
      /// <value>The selected grammar.</value>
      public GrammarReference SelectedGrammar { get; private set; }

      /// <summary>
      /// Gets or sets the grammars to select from.
      /// </summary>
      /// <value>The grammars to select from.</value>
      public IEnumerable<GrammarReference> GrammarsToSelectFrom
      {
         get => _GrammarsToSelectFrom;
         set
         {
            _GrammarsToSelectFrom = value;
            GrammarListView.SetObjects(_GrammarsToSelectFrom);
         }
      }

      private void BtnCancel_Click(object sender, EventArgs e)
      {
         DialogResult = DialogResult.Cancel;
         Close();
      }

      private void BtnOk_Click(object sender, EventArgs e)
      {
         SelectedGrammar = GrammarListView.SelectedItem?.RowObject as GrammarReference;

         if (SelectedGrammar == null)
            return;

         DialogResult = DialogResult.OK;
         Close();
      }
   }
}
