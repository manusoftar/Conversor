using FFMpegCore;
using FFMpegCore.FFMPEG;
using FFMpegCore.FFMPEG.Argument;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Conversor
{
    public partial class Form1 : Form
    {
        String ffmpegPath = "";
        FFMpeg encoder;
        Property config;
        String rutaArchivoConfig = "";

        public Form1()
        {
            InitializeComponent();

            //Verifico si hay archivo de configuración
            String ruta = Application.ExecutablePath;
            String ubicacion = Path.GetDirectoryName(ruta);
            rutaArchivoConfig = ubicacion + "\\config.cfg";

            if (System.IO.File.Exists(rutaArchivoConfig))
            {
                config = new Property(rutaArchivoConfig);
                ffmpegPath = config.get("ffmpegPath");
                textBox2.Text = ffmpegPath;
            }
            

                  
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            
        }


        private void textBox1_click(object sender, EventArgs e)
        {
            folderBrowserDialog1.ShowDialog();
            textBox1.Text = folderBrowserDialog1.SelectedPath.ToString();
        }

        private void addVideoBtn_click(object sender, EventArgs e)
        {
            
            openFileDialog1.Filter = "Archivos de video (*.mp4, *.3gp) | *.mp4; *.3gp";
            openFileDialog1.Title = "Selecciona el video a convertir/comprimir";
            openFileDialog1.Multiselect = true;
            openFileDialog1.ShowDialog();

            foreach (String nombre in openFileDialog1.FileNames) {
                listBox1.Items.Add(nombre);
            }

            textProgressBar4.Maximum = listBox1.Items.Count;

        }

        private void eliminarSeleccionadosBtn_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex != -1){
                
                for (int i= listBox1.SelectedItems.Count -1; i>=0; i--)
                {
                    listBox1.Items.Remove(listBox1.SelectedItems[i]);
                }
                textProgressBar4.Maximum = listBox1.Items.Count;
            }

        }

        private async void button3_Click(object sender, EventArgs e)
        {
            if (encoder!=null && encoder.IsWorking)
            {
                MessageBox.Show("Ya estás procesando uno o más videos, espera que se termine el proceso actual o abórtalo para iniciar un nuevo trabajo",
                                "Información", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            
            int alto = 720, ancho = 1280;
            if (resolucion720.Checked)
            {
                alto = 720;
                ancho = 1280;
            }

            if (resolucion1080.Checked)
            {
                alto = 1080;
                ancho = 1920;
            }

            if (resolucion4K.Checked)
            {
                alto = 2160;
                ancho = 3840;
            }

            if (ffmpegPath != null && ffmpegPath != "")
            {

                FFMpegOptions.Configure(new FFMpegOptions { RootDirectory = Path.GetDirectoryName(ffmpegPath) });
                encoder = new FFMpeg();

                var AudioQuality = FFMpegCore.FFMPEG.Enums.AudioQuality.Hd;
                

                //Attacheo el listener
                encoder.OnProgress += OnProgress2;

                textProgressBar4.Value = 0;

                foreach (String ruta in listBox1.Items)
                {
                    try
                    {
                        textProgressBar3.Value = 0;
                        await Task.Run(() =>
                        {

                            //ffmpeg -i VID_20200202_163129.mp4 -c:v libx264 -b:v 8M -minrate 8M -preset medium -vf scale=1280:720 -c:a aac -b:a 192K VID_20200202_163129_converted.mp4
                            var container = new ArgumentContainer
                            {
                            new InputArgument(new VideoInfo(ruta)),
                            new VideoCodecArgument(FFMpegCore.FFMPEG.Enums.VideoCodec.LibX264,8000),
                            new SpeedArgument(FFMpegCore.FFMPEG.Enums.Speed.Medium),
                            new ScaleArgument(ancho,alto),
                            new AudioCodecArgument(FFMpegCore.FFMPEG.Enums.AudioCodec.Aac, FFMpegCore.FFMPEG.Enums.AudioQuality.Normal),
                            new ThreadsArgument(0),
                            new OutputArgument(new FileInfo(textBox1.Text + "\\" + Path.GetFileNameWithoutExtension(ruta) + "_convertido.mp4"))

                            };

                            encoder.Convert(container);
                        });
                        textProgressBar4.Value++;
                    }
                    catch (Exception ex) { }
                                      
                   
                    
                }
            } else
            {
                MessageBox.Show("Debes indicar la ruta al ejecutable de ffmpeg","Aviso", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            

        }

        private void OnProgress2(double percentage)
        {
            Console.WriteLine("Progress {0}%", percentage);
            Console.WriteLine(percentage);
            if (textProgressBar3.InvokeRequired)
                {
                    textProgressBar3.Invoke(new Action(delegate ()
                    {
                        textProgressBar3.Value = Math.Min(100, Convert.ToInt32(percentage));
                    }));
                }
            
        }

        private void rutaFfmpeg_click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "Ejecutable de FFmpeg | ffmpeg.exe";
            openFileDialog1.Title = "Selecciona la ubicación del ejecutable";
            openFileDialog1.Multiselect = false;
            openFileDialog1.ShowDialog();

            ffmpegPath = openFileDialog1.FileName;
            textBox2.Text = ffmpegPath;

            if (config == null) {
                config = new Property(rutaArchivoConfig);
            }
            config.set("ffmpegPath", ffmpegPath);
            config.Save();
        }

        private void abortarProceso(object sender, FormClosingEventArgs e)
        {
            if (encoder != null)
            {
                encoder.Stop();
                encoder.Kill();
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if(encoder.IsWorking)
            {
                encoder.Stop();
                encoder.Kill();
                textProgressBar3.Value = 0;
            }
        }
    }
}
