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
using System.Reflection;
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
        String installDirectory = "";
        const int BITRATE = 8000; // Bitrate en kbps
        const string DefaultLogFileName = "Conversor.log";

        public Form1()
        {
            InitializeComponent();
            AppLogger.RegisterSink(AppendLogToConsole);

            //Verifico si hay archivo de configuración
            String ruta = Application.ExecutablePath;
            installDirectory = Path.GetDirectoryName(ruta);
            rutaArchivoConfig = Path.Combine(installDirectory, "config.cfg");

            bool configExists = System.IO.File.Exists(rutaArchivoConfig);
            config = new Property(rutaArchivoConfig);
            InicializarConfiguracion(configExists);
            ConfigurarArchivoDeLog();

            ffmpegPath = ResolverRutaFfmpeg();
            textBox2.Text = ffmpegPath;

            // Cargar preferencia de GPU
            String useGPU = config.get("useGPU", "false");
            checkBoxGPU.Checked = useGPU.ToLower() == "true";

            RegistrarDiagnosticosDeInicio();

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
                AppLogger.Exception("Error detectando GPU.", ex);
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
                AppLogger.Exception("Error detectando GPU.", ex);
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

        private void AppendLogToConsole(string msg)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => AppendLogToConsole(msg)));
                return;
            }
            txtLog.AppendText(msg + Environment.NewLine);
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

            string ffmpegValidationError;
            if (!ValidarRutaFfmpeg(ffmpegPath, out ffmpegValidationError))
            {
                MessageBox.Show(ffmpegValidationError, "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppLogger.Warn(ffmpegValidationError);
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

            AppLogger.Info("=== Inicio del proceso ===");
            AppLogger.Info("FFmpeg: " + ffmpegPath);
            AppLogger.Info("Destino: " + textBox1.Text);
            AppLogger.Info("Resolución: " + ancho + "x" + alto);
            AppLogger.Info("GPU habilitada: " + (checkBoxGPU.Checked && checkBoxGPU.Enabled ? "si" : "no"));
            AppLogger.Info("Videos a procesar: " + listBox1.Items.Count);

            try
            {
                foreach (String ruta in listBox1.Items)
                {
                    textProgressBar3.Value = 0;
                    String outputPath = textBox1.Text + "\\" + Path.GetFileNameWithoutExtension(ruta) + "_convertido.mp4";
                    bool conversionExitosa = false;

                    AppLogger.Info("--- Procesando: " + Path.GetFileName(ruta));
                    AppLogger.Info("    Entrada: " + ruta);
                    AppLogger.Info("    Salida:  " + outputPath);

                    // Obtener duración del video para el progreso
                    TimeSpan duration;
                    try
                    {
                        AppLogger.Info("    Analizando video con ffprobe...");
                        var mediaInfo = await Task.Run(() => FFProbe.Analyse(ruta));
                        duration = mediaInfo.Duration;
                        
                        if (duration.TotalSeconds <= 0)
                        {
                            AppLogger.Warn("    Duración inválida, se omitirá este archivo");
                            MessageBox.Show($"No se pudo obtener la duración del video: {Path.GetFileName(ruta)}.\nSe omitirá este archivo.",
                                           "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            textProgressBar4.Value++;
                            continue;
                        }
                        AppLogger.Info("    Duración: " + duration.ToString(@"hh\:mm\:ss"));
                    }
                    catch (Exception exProbe)
                    {
                        AppLogger.Exception("No se pudo analizar el video: " + Path.GetFileName(ruta), exProbe);
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

                                AppLogger.Info("    GPU (" + tipoGPU + ") codec: " + customCodecArgs);
                                AppLogger.Info("    Iniciando conversión con GPU...");

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
                                AppLogger.Info("    [OK] Conversión con GPU completada");
                            }
                            catch (Exception exGPU)
                            {
                                AppLogger.Exception("Falló la conversión con GPU.", exGPU);
                                AppLogger.Info("    Intentando con CPU como alternativa...");
                                MessageBox.Show("La conversión con GPU falló. Se intentará con CPU.",
                                               "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }
                        else
                        {
                            AppLogger.Warn("    No se detectó GPU compatible, usando CPU");
                            MessageBox.Show("No se detectó una GPU compatible (NVIDIA, AMD o Intel). Se usará CPU.",
                                           "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }

                    // Fallback a CPU si GPU falló o no está habilitado
                    if (!conversionExitosa)
                    {
                        try
                        {
                            AppLogger.Info("    CPU codec: libx264, bitrate=" + BITRATE + "k, escala=" + ancho + "x" + alto + ", preset=medium");
                            AppLogger.Info("    Iniciando conversión con CPU...");

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
                            AppLogger.Info("    [OK] Conversión con CPU completada");
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Exception("Conversión fallida para el video: " + Path.GetFileName(ruta), ex);
                            MessageBox.Show("Error al convertir el video: " + ex.Message,
                                           "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                                      
                    textProgressBar4.Value++;
                }

                AppLogger.Info("=== Proceso finalizado ===");
            }
            catch (Exception exGlobal)
            {
                AppLogger.Exception("Error inesperado durante el proceso.", exGlobal);
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

            if (string.IsNullOrWhiteSpace(openFileDialog1.FileName))
                return;

            string validationError;
            if (!ValidarRutaFfmpeg(openFileDialog1.FileName, out validationError))
            {
                AppLogger.Warn(validationError);
                MessageBox.Show(validationError, "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ffmpegPath = openFileDialog1.FileName;
            textBox2.Text = ffmpegPath;

            config.set("ffmpegPath", ffmpegPath);
            GuardarConfiguracion();
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
            config.set("useGPU", checkBoxGPU.Checked.ToString().ToLower());
            GuardarConfiguracion();
        }

        private void InicializarConfiguracion(bool configExists)
        {
            bool changed = false;
            changed |= AsegurarValorConfiguracion("ffmpegPath", "");
            changed |= AsegurarValorConfiguracion("useGPU", "false");
            changed |= AsegurarValorConfiguracion("enableFileLogging", "false");
            changed |= AsegurarValorConfiguracion("logFilePath", Path.Combine(installDirectory, DefaultLogFileName));

            if (changed)
                GuardarConfiguracion();

            if (!configExists)
                AppLogger.Info("No se encontró config.cfg. Se creó uno nuevo con valores por defecto.");
        }

        private bool AsegurarValorConfiguracion(string key, string value)
        {
            if (config.get(key) != null)
                return false;

            config.set(key, value);
            return true;
        }

        private void ConfigurarArchivoDeLog()
        {
            bool enableFileLogging = config.get("enableFileLogging", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
            string logPath = NormalizarRuta(config.get("logFilePath", Path.Combine(installDirectory, DefaultLogFileName)));

            config.set("logFilePath", logPath);
            GuardarConfiguracion();

            AppLogger.ConfigureFileLogging(enableFileLogging, logPath);

            if (enableFileLogging)
                AppLogger.Info("Log a archivo habilitado: " + logPath);
        }

        private string ResolverRutaFfmpeg()
        {
            string configuredPath = config.get("ffmpegPath", "");
            string detectedPath = DetectarRutaFfmpeg(configuredPath);

            if (!String.Equals(configuredPath ?? "", detectedPath ?? "", StringComparison.OrdinalIgnoreCase))
            {
                config.set("ffmpegPath", detectedPath ?? "");
                GuardarConfiguracion();
            }

            return detectedPath ?? "";
        }

        private string DetectarRutaFfmpeg(string configuredPath)
        {
            string validationError;
            string bundledPath = Path.Combine(installDirectory, "ffmpeg.exe");

            if (ValidarRutaFfmpeg(bundledPath, out validationError))
            {
                AppLogger.Info("Se usará el FFmpeg empaquetado junto a la aplicación.");
                return bundledPath;
            }

            if (!String.IsNullOrWhiteSpace(configuredPath) && ValidarRutaFfmpeg(configuredPath, out validationError))
                return configuredPath;

            if (!String.IsNullOrWhiteSpace(configuredPath))
                AppLogger.Warn("La ruta configurada para FFmpeg no es válida: " + validationError);

            string pathExecutable = BuscarEnPath("ffmpeg.exe");
            if (ValidarRutaFfmpeg(pathExecutable, out validationError))
            {
                AppLogger.Info("Se encontró FFmpeg en PATH: " + pathExecutable);
                return pathExecutable;
            }

            return "";
        }

        private string BuscarEnPath(string fileName)
        {
            string pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (String.IsNullOrWhiteSpace(pathVariable))
                return "";

            foreach (string candidateDirectory in pathVariable.Split(Path.PathSeparator))
            {
                if (String.IsNullOrWhiteSpace(candidateDirectory))
                    continue;

                try
                {
                    string candidate = Path.Combine(candidateDirectory.Trim(), fileName);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                }
            }

            return "";
        }

        private bool ValidarRutaFfmpeg(string candidatePath, out string validationError)
        {
            validationError = "Debes indicar una ruta válida al ejecutable de ffmpeg.";

            if (String.IsNullOrWhiteSpace(candidatePath))
                return false;

            string normalizedPath = NormalizarRuta(candidatePath);
            if (!File.Exists(normalizedPath))
            {
                validationError = "No se encontró ffmpeg.exe en la ruta indicada.";
                return false;
            }

            if (!String.Equals(Path.GetFileName(normalizedPath), "ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
            {
                validationError = "Debes seleccionar el archivo ffmpeg.exe.";
                return false;
            }

            string ffprobePath = Path.Combine(Path.GetDirectoryName(normalizedPath), "ffprobe.exe");
            if (!File.Exists(ffprobePath))
            {
                validationError = "No se encontró ffprobe.exe en la misma carpeta que ffmpeg.exe.";
                return false;
            }

            return true;
        }

        private string NormalizarRuta(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                return path;

            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            return Path.GetFullPath(Path.Combine(installDirectory, path));
        }

        private void GuardarConfiguracion()
        {
            try
            {
                config.Save();
            }
            catch (Exception ex)
            {
                AppLogger.Exception("No se pudo guardar el archivo de configuración.", ex);
            }
        }

        private void RegistrarDiagnosticosDeInicio()
        {
            Assembly ffmpegCoreAssembly = typeof(FFMpegArguments).Assembly;
            AppLogger.Info("Conversor iniciado en: " + installDirectory);
            AppLogger.Info("FFMpegCore ensamblado: " + ffmpegCoreAssembly.GetName().Version + " (" + ffmpegCoreAssembly.Location + ")");

            if (String.IsNullOrWhiteSpace(ffmpegPath))
                AppLogger.Warn("No se encontró una instalación válida de FFmpeg. Selecciona ffmpeg.exe manualmente.");
            else
                AppLogger.Info("Ruta activa de FFmpeg: " + ffmpegPath);
        }
    }
}
