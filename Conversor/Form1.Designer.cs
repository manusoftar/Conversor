using System;
using TextProgressBar;

namespace Conversor
{
    partial class Form1
    {
        /// <summary>
        /// Variable del diseñador necesaria.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpiar los recursos que se estén usando.
        /// </summary>
        /// <param name="disposing">true si los recursos administrados se deben desechar; false en caso contrario.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código generado por el Diseñador de Windows Forms

        /// <summary>
        /// Método necesario para admitir el Diseñador. No se puede modificar
        /// el contenido de este método con el editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.label1 = new System.Windows.Forms.Label();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.label2 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.resolucion4K = new System.Windows.Forms.RadioButton();
            this.resolucion1080 = new System.Windows.Forms.RadioButton();
            this.resolucion720 = new System.Windows.Forms.RadioButton();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.textProgressBar3 = new TextProgressBar.TextProgressBar();
            this.textProgressBar4 = new TextProgressBar.TextProgressBar();
            this.button4 = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 81);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(54, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Orígenes:";
            // 
            // listBox1
            // 
            this.listBox1.FormattingEnabled = true;
            this.listBox1.HorizontalScrollbar = true;
            this.listBox1.Location = new System.Drawing.Point(15, 97);
            this.listBox1.Name = "listBox1";
            this.listBox1.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBox1.Size = new System.Drawing.Size(203, 264);
            this.listBox1.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(241, 81);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(46, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Destino:";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(244, 97);
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Horizontal;
            this.textBox1.Size = new System.Drawing.Size(544, 20);
            this.textBox1.TabIndex = 3;
            this.toolTip1.SetToolTip(this.textBox1, "Haz click para seleccionar la carpeta de destino");
            this.textBox1.Click += new System.EventHandler(this.textBox1_click);
            this.textBox1.TextChanged += new System.EventHandler(this.textBox1_TextChanged);
            // 
            // toolTip1
            // 
            this.toolTip1.Tag = "";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.resolucion4K);
            this.groupBox1.Controls.Add(this.resolucion1080);
            this.groupBox1.Controls.Add(this.resolucion720);
            this.groupBox1.Location = new System.Drawing.Point(244, 141);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(544, 112);
            this.groupBox1.TabIndex = 7;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Resoluciones";
            this.toolTip1.SetToolTip(this.groupBox1, "Selecciona la resolución del video que se generará, \r\ncuidado si el video de orig" +
        "en es de menor resolución\r\nque la seleccionada, el resultado será un video pixel" +
        "ado y\r\ndeformado!!");
            // 
            // resolucion4K
            // 
            this.resolucion4K.AutoSize = true;
            this.resolucion4K.Location = new System.Drawing.Point(24, 79);
            this.resolucion4K.Name = "resolucion4K";
            this.resolucion4K.Size = new System.Drawing.Size(115, 17);
            this.resolucion4K.TabIndex = 2;
            this.resolucion4K.Text = "3840 x 2160  --  4K";
            this.resolucion4K.UseVisualStyleBackColor = true;
            // 
            // resolucion1080
            // 
            this.resolucion1080.AutoSize = true;
            this.resolucion1080.Location = new System.Drawing.Point(24, 56);
            this.resolucion1080.Name = "resolucion1080";
            this.resolucion1080.Size = new System.Drawing.Size(129, 17);
            this.resolucion1080.TabIndex = 1;
            this.resolucion1080.Text = "1920 x 1080  -- 1080p";
            this.resolucion1080.UseVisualStyleBackColor = true;
            // 
            // resolucion720
            // 
            this.resolucion720.AutoSize = true;
            this.resolucion720.Checked = true;
            this.resolucion720.Location = new System.Drawing.Point(24, 33);
            this.resolucion720.Name = "resolucion720";
            this.resolucion720.Size = new System.Drawing.Size(117, 17);
            this.resolucion720.TabIndex = 0;
            this.resolucion720.TabStop = true;
            this.resolucion720.Text = "1280 x 720  -- 720p";
            this.resolucion720.UseVisualStyleBackColor = true;
            // 
            // textBox2
            // 
            this.textBox2.Location = new System.Drawing.Point(15, 38);
            this.textBox2.Name = "textBox2";
            this.textBox2.ReadOnly = true;
            this.textBox2.Size = new System.Drawing.Size(773, 20);
            this.textBox2.TabIndex = 12;
            this.toolTip1.SetToolTip(this.textBox2, "Aquí debes seleccionar la ruta donde se encuentra el archivo ffmpeg.exe (es el co" +
        "mpresor)");
            this.textBox2.Click += new System.EventHandler(this.rutaFfmpeg_click);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            this.openFileDialog1.Multiselect = true;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(15, 367);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(203, 23);
            this.button1.TabIndex = 5;
            this.button1.Text = "Agregar video";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.addVideoBtn_click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(15, 396);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(203, 23);
            this.button2.TabIndex = 6;
            this.button2.Text = "Eliminar seleccionados";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.eliminarSeleccionadosBtn_Click);
            // 
            // button3
            // 
            this.button3.Font = new System.Drawing.Font("MS Reference Sans Serif", 21.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button3.Location = new System.Drawing.Point(244, 259);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(544, 102);
            this.button3.TabIndex = 8;
            this.button3.Text = "PROCESAR";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 22);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(97, 13);
            this.label3.TabIndex = 11;
            this.label3.Text = "Ruta a ffmpeg.exe:";
            // 
            // textProgressBar3
            // 
            this.textProgressBar3.CustomText = "";
            this.textProgressBar3.Location = new System.Drawing.Point(15, 437);
            this.textProgressBar3.Name = "textProgressBar3";
            this.textProgressBar3.ProgressColor = System.Drawing.Color.LightGreen;
            this.textProgressBar3.Size = new System.Drawing.Size(773, 23);
            this.textProgressBar3.TabIndex = 13;
            this.textProgressBar3.TextColor = System.Drawing.Color.Black;
            this.textProgressBar3.TextFont = new System.Drawing.Font("Times New Roman", 11F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic))));
            this.textProgressBar3.VisualMode = TextProgressBar.ProgressBarDisplayMode.Percentage;
            // 
            // textProgressBar4
            // 
            this.textProgressBar4.CustomText = "";
            this.textProgressBar4.Location = new System.Drawing.Point(15, 476);
            this.textProgressBar4.Name = "textProgressBar4";
            this.textProgressBar4.ProgressColor = System.Drawing.Color.LightGreen;
            this.textProgressBar4.Size = new System.Drawing.Size(773, 23);
            this.textProgressBar4.TabIndex = 14;
            this.textProgressBar4.TextColor = System.Drawing.Color.Black;
            this.textProgressBar4.TextFont = new System.Drawing.Font("Times New Roman", 11F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic))));
            this.textProgressBar4.VisualMode = TextProgressBar.ProgressBarDisplayMode.CurrProgress;
            // 
            // button4
            // 
            this.button4.Font = new System.Drawing.Font("MS Reference Sans Serif", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button4.Location = new System.Drawing.Point(244, 367);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(544, 52);
            this.button4.TabIndex = 15;
            this.button4.Text = "ABORTAR";
            this.button4.UseVisualStyleBackColor = true;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 511);
            this.Controls.Add(this.button4);
            this.Controls.Add(this.textProgressBar4);
            this.Controls.Add(this.textProgressBar3);
            this.Controls.Add(this.textBox2);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.listBox1);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "Form1";
            this.Text = "Conversor de Videos";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.abortarProceso);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private void userControl1_Load(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton resolucion4K;
        private System.Windows.Forms.RadioButton resolucion1080;
        private System.Windows.Forms.RadioButton resolucion720;
        private System.Windows.Forms.Button button3;      
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBox2;
        private TextProgressBar.TextProgressBar textProgressBar3;
        private TextProgressBar.TextProgressBar textProgressBar4;
        private System.Windows.Forms.Button button4;
    }
}

