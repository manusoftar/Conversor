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

            // Detectar GPU y actualizar estado (antes de conectar el evento para no disparar guardado)
            InicializarEstadoGPU();

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
                AppendLog("[WARN] Error detectando GPU: " + ex.Message);
            }
            return null;
        }

        private String DetectarNombreGPU()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("select * from Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        String nombre = obj["Name"]?.ToString() ?? "";
                        String nombreUpper = nombre.ToUpper();
                        if (nombreUpper.Contains("NVIDIA") || nombreUpper.Contains("AMD") ||
                            nombreUpper.Contains("RADEON") || nombreUpper.Contains("INTEL"))
                        {
                            return nombre;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog("[WARN] Error detectando GPU: " + ex.Message);
            }
            return null;
        }

        private void InicializarEstadoGPU()
        {
            String gpuName = DetectarNombreGPU();
            if (gpuName != null)
            {
                checkBoxGPU.Enabled = true;
                String gpuUpper = gpuName.ToUpper();
                String encoder = gpuUpper.Contains("NVIDIA") ? "NVENC"
                               : (gpuUpper.Contains("AMD") || gpuUpper.Contains("RADEON")) ? "AMF"
                               : gpuUpper.Contains("INTEL") ? "QSV" : "";
                lblGpuStatus.Text = gpuName + (encoder.Length > 0 ? " - " + encoder + " disponible" : "");
                lblGpuStatus.ForeColor = System.Drawing.Color.DarkGreen;
            }
            else
            {
                checkBoxGPU.Enabled = false;
                checkBoxGPU.Checked = false;
                lblGpuStatus.Text = "Sin GPU compatible detectada - se usará CPU";
                lblGpuStatus.ForeColor = System.Drawing.Color.Red;
            }
        }

        private void AppendLog(string msg)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => AppendLog(msg)));
                return;
            }
            txtLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine);
            txtLog.ScrollToCaret();
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            if (isProcessing)
            {
                MessageBox.Show("Ya estás procesando uno o más videos, espera que se termine el proceso actual o abórtalo para iniciar un nuevo trabajo",
                                "Información", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Validaciones previas
            if (string.IsNullOrWhiteSpace(ffmpegPath))
            {
                MessageBox.Show("Debes indicar la ruta al ejecutable de ffmpeg", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (listBox1.Items.Count == 0)
            {
                MessageBox.Show("Debes agregar al menos un video a la lista de orígenes", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(textBox1.Text))
            {
                MessageBox.Show("Debes seleccionar la carpeta de destino", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            GlobalFFOptions.Configure(new FFOptions { BinaryFolder = Path.GetDirectoryName(ffmpegPath) });

            textProgressBar4.Value = 0;
            isProcessing = true;
            button3.Enabled = false;
            txtLog.Clear();

            AppendLog("=== Inicio del proceso ===");
            AppendLog("FFmpeg: " + ffmpegPath);
            AppendLog("Destino: " + textBox1.Text);
            AppendLog("Resolución: " + ancho + "x" + alto);
            AppendLog("GPU habilitada: " + (checkBoxGPU.Checked && checkBoxGPU.Enabled ? "si" : "no"));
            AppendLog("Videos a procesar: " + listBox1.Items.Count);

            try
            {
                foreach (String ruta in listBox1.Items)
                {
                    textProgressBar3.Value = 0;
                    String outputPath = textBox1.Text + "\\" + Path.GetFileNameWithoutExtension(ruta) + "_convertido.mp4";
                    bool conversionExitosa = false;

                    AppendLog("--- Procesando: " + Path.GetFileName(ruta));
                    AppendLog("    Entrada: " + ruta);
                    AppendLog("    Salida:  " + outputPath);

                    // Obtener duración del video para el progreso
                    TimeSpan duration;
                    try
                    {
                        AppendLog("    Analizando video con ffprobe...");
                        var mediaInfo = await Task.Run(() => FFProbe.Analyse(ruta));
                        duration = mediaInfo.Duration;
                        
                        if (duration.TotalSeconds <= 0)
                        {
                            AppendLog("    [AVISO] Duración inválida, se omitirá este archivo");
                            MessageBox.Show($"No se pudo obtener la duración del video: {Path.GetFileName(ruta)}.\nSe omitirá este archivo.",
                                           "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            textProgressBar4.Value++;
                            continue;
                        }
                        AppendLog("    Duración: " + duration.ToString(@"hh\:mm\:ss"));
                    }
                    catch (Exception exProbe)
                    {
                        AppendLog("    [ERROR] No se pudo analizar el video: " + exProbe.Message);
                        MessageBox.Show($"Error al analizar el video: {Path.GetFileName(ruta)}\n{exProbe.Message}\nSe omitirá este archivo.",
                                       "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        textProgressBar4.Value++;
                        continue;
                    }

                    // Intentar con GPU si está habilitado y disponible
                    if (checkBoxGPU.Checked && checkBoxGPU.Enabled)
                    {
                        String tipoGPU = DetectarGPU();
                        if (tipoGPU != null)
                        {
                            try
                            {
                                string customCodecArgs = "";
                                
                                if (tipoGPU == "NVIDIA")
                                    customCodecArgs = "-c:v h264_nvenc -preset slow -rc vbr_hq -b:v " + BITRATE + "k";
                                else if (tipoGPU == "AMD")
                                    customCodecArgs = "-c:v h264_amf -quality quality -b:v " + BITRATE + "k";
                                else if (tipoGPU == "INTEL")
                                    customCodecArgs = "-c:v h264_qsv -preset slow -b:v " + BITRATE + "k";

                                AppendLog("    GPU (" + tipoGPU + ") codec: " + customCodecArgs);
                                AppendLog("    Iniciando conversión con GPU...");

                                string capturedCodecArgs = customCodecArgs;
                                await Task.Run(() =>
                                {
                                    FFMpegArguments
                                        .FromFileInput(ruta)
                                        .OutputToFile(outputPath, true, options => options
                                            .WithCustomArgument("-vf scale=" + ancho + ":" + alto)
                                            .WithCustomArgument(capturedCodecArgs)
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
                                AppendLog("    [OK] Conversión con GPU completada");
                            }
                            catch (Exception exGPU)
                            {
                                AppendLog("    [AVISO] GPU fallo: " + exGPU.Message);
                                AppendLog("    Intentando con CPU como alternativa...");
                                MessageBox.Show("La conversión con GPU falló. Se intentará con CPU.",
                                               "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }
                        else
                        {
                            AppendLog("    [AVISO] No se detectó GPU compatible, usando CPU");
                            MessageBox.Show("No se detectó una GPU compatible (NVIDIA, AMD o Intel). Se usará CPU.",
                                           "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }

                    // Fallback a CPU si GPU falló o no está habilitado
                    if (!conversionExitosa)
                    {
                        try
                        {
                            AppendLog("    CPU codec: libx264, bitrate=" + BITRATE + "k, escala=" + ancho + "x" + alto + ", preset=medium");
                            AppendLog("    Iniciando conversión con CPU...");

                            await Task.Run(() =>
                            {
                                FFMpegArguments
                                    .FromFileInput(ruta)
                                    .OutputToFile(outputPath, true, options => options
                                        .WithVideoCodec(VideoCodec.LibX264)
                                        .WithVideoBitrate(BITRATE)
                                        .WithCustomArgument("-vf scale=" + ancho + ":" + alto)
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
                            AppendLog("    [OK] Conversión con CPU completada");
                        }
                        catch (Exception ex)
                        {
                            AppendLog("    [ERROR] Conversión fallida: " + ex.Message);
                            MessageBox.Show("Error al convertir el video: " + ex.Message,
                                           "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                                      
                    textProgressBar4.Value++;
                }

                AppendLog("=== Proceso finalizado ===");
            }
            catch (Exception exGlobal)
            {
                AppendLog("[ERROR CRITICO] " + exGlobal.Message);
                MessageBox.Show("Error inesperado durante el proceso: " + exGlobal.Message,
                               "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isProcessing = false;
                button3.Enabled = true;
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
