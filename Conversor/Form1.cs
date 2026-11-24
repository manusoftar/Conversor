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
        String dataDirectory = ""; // carpeta donde viven config.cfg y el log (instalación, o LocalAppData si no es escribible)
        const int BITRATE = 8000; // Bitrate en kbps
        const string DefaultLogFileName = "Conversor.log";
        const int MaxVideosEnParalelo = 2;

        // Estado del proceso de conversión en curso
        System.Threading.CancellationTokenSource cancelSource;
        volatile bool abortando = false;
        readonly object progresoLock = new object();
        readonly Dictionary<int, int> progresoActivos = new Dictionary<int, int>(); // índice de video -> % actual

        public Form1()
        {
            InitializeComponent();
            AppLogger.RegisterSink(AppendLogToConsole);

            // Si Conversor termina por cualquier motivo, Windows cierra también sus ffmpeg/ffprobe
            ProcessJob.KillChildrenOnExit();
            button4.Enabled = false; // ABORTAR solo se habilita mientras se procesa

            //Verifico si hay archivo de configuración
            String ruta = Application.ExecutablePath;
            installDirectory = Path.GetDirectoryName(ruta);
            rutaArchivoConfig = ElegirRutaConfiguracion(installDirectory);
            dataDirectory = Path.GetDirectoryName(rutaArchivoConfig);

            bool configExists = System.IO.File.Exists(rutaArchivoConfig);
            config = new Property(rutaArchivoConfig);
            InicializarConfiguracion(configExists);
            ConfigurarArchivoDeLog();

            ffmpegPath = ResolverRutaFfmpeg();
            textBox2.Text = ffmpegPath;

            // Cargar preferencia de GPU
            String useGPU = config.get("useGPU", "false");
            checkBoxGPU.Checked = useGPU.ToLower() == "true";

            // Cargar preferencia de procesamiento paralelo
            String paralelo = config.get("parallelProcessing", "false");
            checkBoxParalelo.Checked = paralelo.ToLower() == "true";

            RegistrarDiagnosticosDeInicio();

            // Detectar GPU y actualizar estado (antes de conectar el evento para no disparar guardado)
            InicializarEstadoGPU();

            // Agregar event handler para guardar cambios del checkbox
            checkBoxGPU.CheckedChanged += checkBoxGPU_CheckedChanged;
            checkBoxParalelo.CheckedChanged += checkBoxParalelo_CheckedChanged;
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
            if (txtLog.IsDisposed || txtLog.Disposing)
                return;

            if (txtLog.InvokeRequired)
            {
                try
                {
                    txtLog.Invoke(new Action(() => AppendLogToConsole(msg)));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { } // el formulario se está cerrando
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

            // Datos que usan los workers, capturados acá para no tocar controles desde otros hilos
            List<string> rutas = listBox1.Items.Cast<string>().ToList();
            string carpetaDestino = textBox1.Text;
            bool usarGpu = checkBoxGPU.Checked && checkBoxGPU.Enabled;
            string tipoGPU = usarGpu ? DetectarGPU() : null;
            int simultaneos = checkBoxParalelo.Checked ? Math.Min(MaxVideosEnParalelo, rutas.Count) : 1;
            List<string> salidas = CalcularRutasDeSalida(rutas, carpetaDestino);
            ResumenProceso resumen = new ResumenProceso();

            lock (progresoLock)
                progresoActivos.Clear();
            textProgressBar3.Value = 0;
            textProgressBar4.Value = 0;
            textProgressBar4.Maximum = rutas.Count;
            isProcessing = true;
            abortando = false;
            button3.Enabled = false;
            button4.Enabled = true;
            button4.Text = "ABORTAR";
            txtLog.Clear();

            cancelSource = new System.Threading.CancellationTokenSource();
            System.Threading.CancellationToken token = cancelSource.Token;
            bool trabajoTerminado = false;

            AppLogger.Info("=== Inicio del proceso ===");
            AppLogger.Info("FFmpeg: " + ffmpegPath);
            AppLogger.Info("Destino: " + carpetaDestino);
            AppLogger.Info("Resolución: " + ancho + "x" + alto);
            AppLogger.Info("GPU habilitada: " + (tipoGPU != null ? "si (" + tipoGPU + ")" : "no"));
            AppLogger.Info("Videos a procesar: " + rutas.Count);
            AppLogger.Info("Videos en simultáneo: " + simultaneos);

            if (usarGpu && tipoGPU == null)
            {
                AppLogger.Warn("No se detectó GPU compatible (NVIDIA, AMD o Intel), se usará CPU");
                resumen.Avisar("No se detectó una GPU compatible (NVIDIA, AMD o Intel): se usó CPU.");
            }

            System.Collections.Concurrent.ConcurrentQueue<int> cola =
                new System.Collections.Concurrent.ConcurrentQueue<int>(Enumerable.Range(0, rutas.Count));

            Task reaper = null;
            try
            {
                // Mientras se está abortando, vigila que no quede ningún ffmpeg/ffprobe de este proceso
                // (cubre el caso de un worker que justo estaba arrancando uno nuevo cuando se pidió abortar)
                reaper = Task.Run(async () =>
                {
                    while (!trabajoTerminado)
                    {
                        if (token.IsCancellationRequested)
                            MatarProcesosHijos();
                        await Task.Delay(300);
                    }
                });

                // Pool de workers: cada uno toma el siguiente video de la cola apenas termina el suyo,
                // así siempre hay 'simultaneos' videos en curso mientras queden dos o más por procesar,
                // y el último video se procesa solo.
                List<Task> workers = new List<Task>();
                for (int w = 0; w < simultaneos; w++)
                {
                    workers.Add(Task.Run(async () =>
                    {
                        int indice;
                        while (!token.IsCancellationRequested && cola.TryDequeue(out indice))
                        {
                            ResultadoVideo resultado = await ProcesarVideoAsync(
                                indice, rutas.Count, rutas[indice], salidas[indice], ancho, alto, tipoGPU, resumen, token);
                            resumen.Registrar(resultado);
                        }
                    }));
                }

                await Task.WhenAll(workers);

                if (token.IsCancellationRequested)
                    AppLogger.Warn("=== Proceso ABORTADO === completados: " + resumen.Ok + ", cancelados: " + resumen.Cancelados + ", sin iniciar: " + cola.Count);
                else
                    AppLogger.Info("=== Proceso finalizado === correctos: " + resumen.Ok + ", omitidos: " + resumen.Omitidos + ", con error: " + resumen.Fallidos);
            }
            catch (Exception exGlobal)
            {
                AppLogger.Exception("Error inesperado durante el proceso.", exGlobal);
                resumen.Avisar("Error inesperado durante el proceso: " + exGlobal.Message);
            }
            finally
            {
                trabajoTerminado = true;
                if (reaper != null)
                {
                    try { await reaper; } catch (Exception) { }
                }

                // Red de seguridad: no debe quedar ningún ffmpeg/ffprobe de este proceso
                int sobrantes = MatarProcesosHijos();
                if (sobrantes > 0)
                    AppLogger.Warn("Se cerraron " + sobrantes + " proceso(s) de ffmpeg/ffprobe que habían quedado abiertos.");

                try { cancelSource.Dispose(); } catch (Exception) { }
                cancelSource = null;
                lock (progresoLock)
                    progresoActivos.Clear();

                isProcessing = false;
                abortando = false;
                if (!IsDisposed)
                {
                    textProgressBar3.Value = 0;
                    button3.Enabled = true;
                    button4.Enabled = false;
                    button4.Text = "ABORTAR";
                }
            }

            if (!IsDisposed && !token.IsCancellationRequested)
            {
                List<string> avisos = resumen.ObtenerAvisos();
                if (avisos.Count > 0)
                {
                    string detalle = string.Join(Environment.NewLine, avisos.Take(15).Select(a => "• " + a));
                    if (avisos.Count > 15)
                        detalle += Environment.NewLine + "… y " + (avisos.Count - 15) + " más (ver el log).";
                    MessageBox.Show("El proceso terminó con avisos:" + Environment.NewLine + Environment.NewLine + detalle,
                                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private enum ResultadoVideo { Ok, Omitido, Fallido, Cancelado }

        /// <summary>Acumula resultados y avisos desde varios hilos.</summary>
        private sealed class ResumenProceso
        {
            private readonly object sync = new object();
            private readonly List<string> avisos = new List<string>();
            public int Ok, Omitidos, Fallidos, Cancelados;

            public void Registrar(ResultadoVideo r)
            {
                lock (sync)
                {
                    switch (r)
                    {
                        case ResultadoVideo.Ok: Ok++; break;
                        case ResultadoVideo.Omitido: Omitidos++; break;
                        case ResultadoVideo.Fallido: Fallidos++; break;
                        default: Cancelados++; break;
                    }
                }
            }

            public void Avisar(string mensaje)
            {
                lock (sync)
                    avisos.Add(mensaje);
            }

            public List<string> ObtenerAvisos()
            {
                lock (sync)
                    return new List<string>(avisos);
            }
        }

        /// <summary>
        /// Nombre de salida de cada video (&lt;nombre&gt;_convertido.mp4). Si dos orígenes darían el mismo nombre
        /// (mismo archivo en carpetas distintas) se agrega un sufijo para que no se pisen entre sí,
        /// algo que con dos videos en paralelo corrompería el resultado.
        /// </summary>
        private static List<string> CalcularRutasDeSalida(List<string> rutas, string carpetaDestino)
        {
            HashSet<string> usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> salidas = new List<string>();

            foreach (string ruta in rutas)
            {
                string baseName = Path.GetFileNameWithoutExtension(ruta) + "_convertido";
                string candidato = Path.Combine(carpetaDestino, baseName + ".mp4");
                int n = 2;
                while (!usados.Add(candidato))
                {
                    candidato = Path.Combine(carpetaDestino, baseName + "_" + n + ".mp4");
                    n++;
                }
                salidas.Add(candidato);
            }
            return salidas;
        }

        private async Task<ResultadoVideo> ProcesarVideoAsync(int indice, int total, string ruta, string outputPath,
            int ancho, int alto, string tipoGPU, ResumenProceso resumen, System.Threading.CancellationToken token)
        {
            string nombre = Path.GetFileName(ruta);
            string tag = "[" + (indice + 1) + "/" + total + "] ";
            ResultadoVideo resultado = ResultadoVideo.Fallido;

            try
            {
                token.ThrowIfCancellationRequested();

                AppLogger.Info(tag + "--- Procesando: " + nombre);
                AppLogger.Info(tag + "    Entrada: " + ruta);
                AppLogger.Info(tag + "    Salida:  " + outputPath);
                ReportarProgresoVideo(indice, 0);

                // Obtener duración del video para el progreso
                TimeSpan duration;
                try
                {
                    AppLogger.Info(tag + "    Analizando video con ffprobe...");
                    var mediaInfo = await Task.Run(() => FFProbe.Analyse(ruta));
                    duration = mediaInfo.Duration;
                }
                catch (Exception exProbe) when (!token.IsCancellationRequested)
                {
                    AppLogger.Exception(tag + "No se pudo analizar el video: " + nombre, exProbe);
                    resumen.Avisar("No se pudo analizar '" + nombre + "': " + exProbe.Message + " (se omitió)");
                    resultado = ResultadoVideo.Omitido;
                    return resultado;
                }

                if (duration.TotalSeconds <= 0)
                {
                    AppLogger.Warn(tag + "    Duración inválida, se omitirá este archivo");
                    resumen.Avisar("No se pudo obtener la duración de '" + nombre + "' (se omitió)");
                    resultado = ResultadoVideo.Omitido;
                    return resultado;
                }
                AppLogger.Info(tag + "    Duración: " + duration.ToString(@"hh\:mm\:ss"));

                bool conversionExitosa = false;

                // Intentar con GPU si está habilitado y disponible
                if (tipoGPU != null)
                {
                    string codecGpu = "";
                    if (tipoGPU == "NVIDIA")
                        codecGpu = "-c:v h264_nvenc -preset slow -rc vbr_hq -b:v " + BITRATE + "k";
                    else if (tipoGPU == "AMD")
                        codecGpu = "-c:v h264_amf -quality quality -b:v " + BITRATE + "k";
                    else if (tipoGPU == "INTEL")
                        codecGpu = "-c:v h264_qsv -preset slow -b:v " + BITRATE + "k";

                    try
                    {
                        AppLogger.Info(tag + "    GPU (" + tipoGPU + ") codec: " + codecGpu);
                        AppLogger.Info(tag + "    Iniciando conversión con GPU...");
                        await EjecutarFfmpegAsync(ruta, outputPath, ancho, alto, duration, codecGpu, indice, token);
                        conversionExitosa = true;
                        AppLogger.Info(tag + "    [OK] Conversión con GPU completada");
                    }
                    catch (Exception exGPU) when (!token.IsCancellationRequested)
                    {
                        AppLogger.Exception(tag + "Falló la conversión con GPU.", exGPU);
                        AppLogger.Info(tag + "    Intentando con CPU como alternativa...");
                        resumen.Avisar("La conversión con GPU falló en '" + nombre + "'; se reintentó con CPU.");
                        ReportarProgresoVideo(indice, 0);
                    }
                }

                // Fallback a CPU si GPU falló o no está habilitado
                if (!conversionExitosa)
                {
                    AppLogger.Info(tag + "    CPU codec: libx264, bitrate=" + BITRATE + "k, escala=" + ancho + "x" + alto + ", preset=medium");
                    AppLogger.Info(tag + "    Iniciando conversión con CPU...");
                    await EjecutarFfmpegAsync(ruta, outputPath, ancho, alto, duration, null, indice, token);
                    AppLogger.Info(tag + "    [OK] Conversión con CPU completada");
                }

                resultado = ResultadoVideo.Ok;
            }
            catch (OperationCanceledException)
            {
                resultado = ResultadoVideo.Cancelado;
            }
            catch (Exception ex) when (token.IsCancellationRequested)
            {
                // ffmpeg fue cerrado a propósito por el aborto: el error que sale de ahí es esperado
                AppLogger.Info(tag + "    (interrumpido: " + ex.GetType().Name + ")");
                resultado = ResultadoVideo.Cancelado;
            }
            catch (Exception ex)
            {
                AppLogger.Exception(tag + "Conversión fallida para el video: " + nombre, ex);
                resumen.Avisar("Error al convertir '" + nombre + "': " + ex.Message);
                resultado = ResultadoVideo.Fallido;
            }
            finally
            {
                if (resultado == ResultadoVideo.Cancelado)
                {
                    AppLogger.Warn(tag + "Cancelado: " + nombre);
                    EliminarSalidaIncompleta(outputPath, tag);
                }
                else if (resultado == ResultadoVideo.Fallido)
                {
                    EliminarSalidaIncompleta(outputPath, tag);
                }

                QuitarProgresoVideo(indice, resultado != ResultadoVideo.Cancelado);
            }

            return resultado;
        }

        /// <summary>Ejecuta ffmpeg para un video. codecGpu == null significa CPU (libx264).</summary>
        private Task EjecutarFfmpegAsync(string ruta, string outputPath, int ancho, int alto, TimeSpan duration,
            string codecGpu, int indice, System.Threading.CancellationToken token)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                bool ok = FFMpegArguments
                    .FromFileInput(ruta)
                    .OutputToFile(outputPath, true, options =>
                    {
                        if (codecGpu != null)
                        {
                            options
                                .WithCustomArgument("-vf scale=" + ancho + ":" + alto)
                                .WithCustomArgument(codecGpu)
                                .WithAudioCodec(AudioCodec.Aac)
                                .WithCustomArgument("-threads 0");
                        }
                        else
                        {
                            options
                                .WithVideoCodec(VideoCodec.LibX264)
                                .WithVideoBitrate(BITRATE)
                                .WithCustomArgument("-vf scale=" + ancho + ":" + alto)
                                .WithCustomArgument("-preset medium")
                                .WithAudioCodec(AudioCodec.Aac)
                                .WithCustomArgument("-threads 0");
                        }
                    })
                    .NotifyOnProgress(percentage =>
                    {
                        ReportarProgresoVideo(indice, (int)Math.Max(0, Math.Min(100, Math.Round(percentage))));
                    }, duration)
                    .CancellableThrough(token)
                    .ProcessSynchronously();

                token.ThrowIfCancellationRequested();
                if (!ok)
                    throw new InvalidOperationException("ffmpeg terminó con error.");
            });
        }

        private void EliminarSalidaIncompleta(string outputPath, string tag)
        {
            try
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                    AppLogger.Info(tag + "    Se eliminó el archivo de salida incompleto.");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn(tag + "    No se pudo eliminar el archivo incompleto '" + outputPath + "': " + ex.Message);
            }
        }

        // ---- Progreso (se puede llamar desde cualquier hilo) ----

        private void EnUI(Action accion)
        {
            if (IsDisposed || Disposing)
                return;
            try
            {
                if (InvokeRequired)
                    BeginInvoke(accion);
                else
                    accion();
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { } // el formulario se está cerrando
        }

        private void ReportarProgresoVideo(int indice, int porcentaje)
        {
            lock (progresoLock)
                progresoActivos[indice] = porcentaje;
            EnUI(ActualizarBarraVideoActual);
        }

        private void QuitarProgresoVideo(int indice, bool contarComoTerminado)
        {
            lock (progresoLock)
                progresoActivos.Remove(indice);

            EnUI(() =>
            {
                if (contarComoTerminado)
                    textProgressBar4.Value = Math.Min(textProgressBar4.Maximum, textProgressBar4.Value + 1);
                ActualizarBarraVideoActual();
            });
        }

        // Con un solo video activo muestra su avance; con dos en paralelo, el promedio de ambos
        private void ActualizarBarraVideoActual()
        {
            int[] valores;
            lock (progresoLock)
                valores = progresoActivos.Values.ToArray();

            textProgressBar3.Value = valores.Length == 0 ? 0 : Math.Max(0, Math.Min(100, (int)valores.Average()));
        }

        // ---- Abortar ----

        /// <summary>Cancela el proceso en curso y cierra los ffmpeg/ffprobe lanzados por esta aplicación.</summary>
        private void SolicitarAborto()
        {
            if (!isProcessing || abortando)
                return;

            abortando = true;
            button4.Enabled = false;
            button4.Text = "ABORTANDO...";
            AppLogger.Warn("ABORTANDO: se cancela el proceso y se cierran los ffmpeg en curso...");

            try { cancelSource?.Cancel(); }
            catch (ObjectDisposedException) { }

            int cerrados = MatarProcesosHijos();
            if (cerrados > 0)
                AppLogger.Info("Procesos de ffmpeg/ffprobe cerrados: " + cerrados);
        }

        /// <summary>
        /// Cierra los ffmpeg.exe/ffprobe.exe cuyo proceso padre es esta aplicación (nunca toca los que
        /// el usuario tenga abiertos por su cuenta). Devuelve cuántos cerró.
        /// </summary>
        private int MatarProcesosHijos()
        {
            int cerrados = 0;
            try
            {
                int miPid;
                using (System.Diagnostics.Process yo = System.Diagnostics.Process.GetCurrentProcess())
                    miPid = yo.Id;

                using (var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, Name FROM Win32_Process WHERE ParentProcessId = " + miPid))
                using (ManagementObjectCollection hijos = searcher.Get())
                {
                    foreach (ManagementObject hijo in hijos)
                    {
                        using (hijo)
                        {
                            string nombre = (hijo["Name"] ?? "").ToString();
                            if (!nombre.StartsWith("ffmpeg", StringComparison.OrdinalIgnoreCase) &&
                                !nombre.StartsWith("ffprobe", StringComparison.OrdinalIgnoreCase))
                                continue;

                            try
                            {
                                using (System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById(Convert.ToInt32(hijo["ProcessId"])))
                                {
                                    p.Kill();
                                    p.WaitForExit(3000);
                                    cerrados++;
                                }
                            }
                            catch (Exception) { } // ya había terminado
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("No se pudieron revisar los procesos hijos: " + ex.Message);
            }
            return cerrados;
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
            if (!isProcessing)
                return;

            // Si Windows se está apagando o cerrando la sesión no se puede preguntar
            if (e.CloseReason == CloseReason.UserClosing)
            {
                DialogResult r = MessageBox.Show(
                    "Hay videos procesándose. Si cerrás la aplicación se aborta el proceso y los videos en curso quedarán incompletos.\n\n¿Querés cerrar igual?",
                    "Proceso en curso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }

            SolicitarAborto();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SolicitarAborto();
        }

        private void checkBoxGPU_CheckedChanged(object sender, EventArgs e)
        {
            config.set("useGPU", checkBoxGPU.Checked.ToString().ToLower());
            GuardarConfiguracion();
        }

        private void checkBoxParalelo_CheckedChanged(object sender, EventArgs e)
        {
            config.set("parallelProcessing", checkBoxParalelo.Checked.ToString().ToLower());
            GuardarConfiguracion();
        }

        private void InicializarConfiguracion(bool configExists)
        {
            bool changed = false;
            changed |= AsegurarValorConfiguracion("ffmpegPath", "");
            changed |= AsegurarValorConfiguracion("useGPU", "false");
            changed |= AsegurarValorConfiguracion("parallelProcessing", "false");
            changed |= AsegurarValorConfiguracion("enableFileLogging", "false");
            changed |= AsegurarValorConfiguracion("logFilePath", Path.Combine(dataDirectory, DefaultLogFileName));

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
            string logPath = NormalizarRuta(config.get("logFilePath", Path.Combine(dataDirectory, DefaultLogFileName)));

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

        /// <summary>
        /// config.cfg va junto al ejecutable. Si esa carpeta no es escribible (por ejemplo
        /// Program Files, o el archivo es de solo lectura) se usa %LocalAppData%\Conversor.
        /// Si ya existe un config.cfg en LocalAppData de una ejecución anterior, se respeta.
        /// </summary>
        private string ElegirRutaConfiguracion(string installDir)
        {
            string preferida = Path.Combine(installDir, "config.cfg");
            if (PuedeEscribirArchivo(preferida))
                return preferida;

            string alternativa = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Conversor", "config.cfg");

            AppLogger.Warn("No hay permiso de escritura en " + installDir + ". La configuración se guardará en " + alternativa);
            return alternativa;
        }

        private static bool PuedeEscribirArchivo(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    // Abrir para escritura sin modificar el contenido
                    using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)) { }
                    return true;
                }

                // No existe: comprobar que se puede crear en esa carpeta
                string probe = Path.Combine(Path.GetDirectoryName(path), ".write-test-" + Guid.NewGuid().ToString("N"));
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
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
