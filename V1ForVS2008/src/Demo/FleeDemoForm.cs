using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Demo
{
    public partial class FleeDemoForm : Form
    {
        Timer _timer = new Timer();
        int _index;
        Func<double, double, int, Random, double> deleRed;
        Func<double, double, int, Random, double> deleGreen;
        Func<double, double, int, Random, double> deleBlue;
        Rule[] rules = new Rule[]
        {
            new Rule(){
                Name = "Blinds",
                R = "Math.Round(4*x-y*2) % 2 - x",
                G = "(Math.Abs(x+2*y) % 0.75)*10+y/5",
                B = "Math.Round(Math.Sin(Math.Sqrt(x*x+y*y))*3/5)+x/3"
            },
            new Rule(){
                Name = "Bullseye",
                R = "1-Math.Round(x/y*0.5)",
                G = "1-Math.Round(y/x*0.4)",
                B = "Math.Round(Math.Sin(Math.Sqrt(x*x+y*y)*10))"
            },
            new Rule(){
                Name = "Wave",
                R = "Math.Cos(x/2)/2",
                G = "Math.Cos(y/2)/3",
                B = "Math.Round(Math.Sin(Math.Sqrt(x*x*x+y*y)*10))"
            },
            new Rule(){
                Name = "Swirls",
                R = "x*15",
                G = "Math.Cos(x*y*4900)",
                B = "y*15"
            },
            new Rule(){
                Name = "Semi-Random",
                R = "Math.Cos(x) * r.NextDouble()",
                G = "Math.Pow(y,2.0)",
                B = "Math.Pow(x,2.0)"
            },
            new Rule(){
                Name = "Mod",
                R = "Math.Pow(x,2.0) % y",
                G = "y % x",
                B = "x % y"
            },
            new Rule(){
                Name = "Bullseye动画",
                R = "1-Math.Round(x/y*0.5)",
                G = "1-Math.Round(y/x*0.4)",
                B = "Math.Round(Math.Sin(Math.Sqrt(x*x+y*y)*(t%50)))"
            }
        };

        public FleeDemoForm()
        {
            InitializeComponent();

            this.DoubleBuffered = true;

            _timer.Tick += new EventHandler(_timer_Tick);
            _timer.Interval = 40;

            foreach (Rule rule in rules)
            {
                comboBox1.Items.Add(rule.Name);
            }
            comboBox1.SelectedIndex = 0;
        }

        void _timer_Tick(object sender, EventArgs e)
        {
            _index++;
            GetImage();
            //_timer.Stop();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            _index = 0;
            deleRed = Zhucai.TextToDelegate.ExpressionParser.Compile<Func<double, double, int, Random, double>>("(x,y,t,r)=>" + txtRed.Text, "System");
            deleGreen = Zhucai.TextToDelegate.ExpressionParser.Compile<Func<double, double, int, Random, double>>("(x,y,t,r)=>" + txtGreen.Text, "System");
            deleBlue = Zhucai.TextToDelegate.ExpressionParser.Compile<Func<double, double, int, Random, double>>("(x,y,t,r)=>" + txtBlue.Text, "System");

            _timer.Start();
        }

        Random rdm = new Random();
        void GetImage()
        {
            const double mult = 2 * Math.PI / 256.0;

            Bitmap bitmap = new Bitmap(pictureBox1.Width, pictureBox1.Height);
            for (int y = 0; y < bitmap.Height; y++)
            {
                double inputY = (y - 128) * mult;

                for (int x = 0; x < bitmap.Width; x++)
                {
                    double inputX = (x - 128) * mult;

                    //Console.WriteLine("x:{0};y:{1}", inputX, inputY);

                    double red;
                    double green;
                    double blue;

                    int r, g, b;
                    red = deleRed(inputX, inputY, _index, rdm);
                    r = Process(red);

                    green = deleGreen(inputX, inputY, _index, rdm);
                    g = Process(green);

                    blue = deleBlue(inputX, inputY, _index, rdm);
                    b = Process(blue);

                    bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
                }
            }

            pictureBox1.Image = bitmap;
        }

        private int Process(double d)
        {
            if (d < 0 || double.IsNaN(d))
            {
                d = 0;
            }
            else if (d > 1)
            {
                d = 1;
            }

            return (int)(d * 255);
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _timer.Stop();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex > 0)
            {
                Rule rule = rules[comboBox1.SelectedIndex - 1];
                txtRed.Text = rule.R;
                txtGreen.Text = rule.G;
                txtBlue.Text = rule.B;
            }
        }
    }

    class Rule
    {
        public string Name { get; set; }
        public string R { get; set; }
        public string G { get; set; }
        public string B { get; set; }
    }
}
