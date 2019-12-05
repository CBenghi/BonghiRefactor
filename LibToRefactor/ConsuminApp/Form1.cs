using LibToRefactor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ConsuminApp
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var c = new OneClass();
            string s = "";
            c.IntProp = 2;

            var l = new TwoClass();
            l.ClassValue = new OneClass(s);
            var rets = l.PerformGeneric(c);

            l.PerformGeneric2(new Dictionary<string, string>(), new Dictionary<Dictionary<string, string>, string>());
        }
    }
}
