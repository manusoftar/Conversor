using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Conversor
{
    public partial class Form1 : Form
    {
        String ffmpegPath = "";
        bool isProcessing = false;
        Property config;
        String rutaArchivoConfig = "";
        const int BITRATE = 8000; // Bitrate en kbps

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
                
                // Cargar preferencia de GPU
                String useGPU = config.get("useGPU", "false");
                checkBoxGPU.Checked = useGPU.ToLower() == "true";
            }
            
            // Agregar event handler para guardar cambios del checkbox
            checkBoxGPU.CheckedChanged += checkBoxGPU_CheckedChanged;
            

                  
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

        private String DetectarGPU()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("select * from Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        String nombre = obj["Name"]?.ToString() ?? "";
                        nombre = nombre.ToUpper();

                        if (nombre.Contains("NVIDIA"))
                        {
                            return "NVIDIA";
                        }
                        else if (nombre.Contains("AMD") || nombre.Contains("RADEON"))
                        {
                            return "AMD";
                        }
                        else if (nombre.Contains("INTEL"))
                        {
                            return "INTEL";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error detectando GPU: " + ex.Message);
            }
            return null;
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            if (isProcessing)
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

                GlobalFFOptions.Configure(new FFOptions { BinaryFolder = Path.GetDirectoryName(ffmpegPath) });

                textProgressBar4.Value = 0;
                isProcessing = true;

                foreach (String ruta in listBox1.Items)
                {
                    textProgressBar3.Value = 0;
                    String outputPath = textBox1.Text + "\\" + Path.GetFileNameWithoutExtension(ruta) + "_convertido.mp4";
                    bool conversionExitosa = false;

                    // Obtener duración del video para el progreso (una sola vez, de forma asíncrona)
                    TimeSpan duration;
                    try
                    {
                        var mediaInfo = await Task.Run(() => FFProbe.Analyse(ruta));
                        duration = mediaInfo.Duration;
                        
                        // Validar que la duración sea válida
                        if (duration.TotalSeconds <= 0)
                        {
                            MessageBox.Show($"No se pudo obtener la duración del video: {Path.GetFileName(ruta)}.\nSe omitirá este archivo.",
                                           "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            textProgressBar4.Value++;
                            continue;
                        }
                    }
                    catch (Exception exProbe)
                    {
                        MessageBox.Show($"Error al analizar el video: {Path.GetFileName(ruta)}\n{exProbe.Message}\nSe omitirá este archivo.",
                                       "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        textProgressBar4.Value++;
                        continue;
                    }

                    // Intentar con GPU si está habilitado
                    if (checkBoxGPU.Checked)
                    {
                        String tipoGPU = DetectarGPU();
                        if (tipoGPU != null)
                        {
                            try
                            {
                                await Task.Run(() =>
                                {
                                    string customCodecArgs = "";
                                    
                                    // Agregar codec de GPU según el tipo detectado
                                    if (tipoGPU == "NVIDIA")
                                    {
                                        customCodecArgs = $"-c:v h264_nvenc -preset slow -rc vbr_hq -b:v {BITRATE}k";
                                    }
                                    else if (tipoGPU == "AMD")
                                    {
                                        customCodecArgs = $"-c:v h264_amf -quality quality -b:v {BITRATE}k";
                                    }
                                    else if (tipoGPU == "INTEL")
                                    {
                                        customCodecArgs = $"-c:v h264_qsv -preset slow -b:v {BITRATE}k";
                                    }

                                    FFMpegArguments
                                        .FromFileInput(ruta)
                                        .OutputToFile(outputPath, true, options => options
                                            .WithCustomArgument($"-vf scale={ancho}:{alto}")
                                            .WithCustomArgument(customCodecArgs)
                                            .WithAudioCodec(AudioCodec.Aac)
                                            .WithCustomArgument("-threads 0")
                                        )
                                        .NotifyOnProgress(percentage => {
                                            if (textProgressBar3.InvokeRequired)
                                            {
                                                textProgressBar3.Invoke(new Action(() =>
                                                {
                                                    textProgressBar3.Value = Math.Min(100, Convert.ToInt32(percentage));
                                                }));
                                            }
                                        }, duration)
                                        .ProcessSynchronously();
                                });
                                conversionExitosa = true;
                            }
                            catch (Exception exGPU)
                            {
                                Console.WriteLine("Error en conversión con GPU: " + exGPU.Message);
                                MessageBox.Show("La conversión con GPU falló. Se intentará con CPU.",
                                               "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }
                        else
                        {
                            MessageBox.Show("No se detectó una GPU compatible (NVIDIA, AMD o Intel). Se usará CPU.",
                                           "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }

                    // Fallback a CPU si GPU falló o no está habilitado
                    if (!conversionExitosa)
                    {
                        try
                        {
                            await Task.Run(() =>
                            {
                                FFMpegArguments
                                    .FromFileInput(ruta)
                                    .OutputToFile(outputPath, true, options => options
                                        .WithVideoCodec(VideoCodec.LibX264)
                                        .WithVideoBitrate(BITRATE)
                                        .WithCustomArgument($"-vf scale={ancho}:{alto}")
                                        .WithCustomArgument("-preset medium")
                                        .WithAudioCodec(AudioCodec.Aac)
                                        .WithCustomArgument("-threads 0")
                                    )
                                    .NotifyOnProgress(percentage => {
                                        if (textProgressBar3.InvokeRequired)
                                        {
                                            textProgressBar3.Invoke(new Action(() =>
                                            {
                                                textProgressBar3.Value = Math.Min(100, Convert.ToInt32(percentage));
                                            }));
                                        }
                                    }, duration)
                                    .ProcessSynchronously();
                            });
                        }
                        catch (Exception ex) 
                        {
                            Console.WriteLine("Error en conversión: " + ex.Message);
                            MessageBox.Show("Error al convertir el video: " + ex.Message,
                                           "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                                      
                    textProgressBar4.Value++;
                }
                
                isProcessing = false;
            } else
            {
                MessageBox.Show("Debes indicar la ruta al ejecutable de ffmpeg","Aviso", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            // En la nueva API, los procesos se manejan de forma diferente
            // No hay un objeto encoder persistente para detener
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if(isProcessing)
            {
                MessageBox.Show("Para cancelar el proceso, cierra la aplicación",
                               "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                textProgressBar3.Value = 0;
            }
        }

        private void checkBoxGPU_CheckedChanged(object sender, EventArgs e)
        {
            if (config == null)
            {
                config = new Property(rutaArchivoConfig);
            }
            config.set("useGPU", checkBoxGPU.Checked.ToString().ToLower());
            config.Save();
        }
    }
}
