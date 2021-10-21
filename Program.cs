using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace AutoCCX
{
    class Program
    {
        private Process m_proc;
        private long m_procId = 0;
        private long m_affinity = 0;
        private DateTime lastTime;
        private TimeSpan lastTotalProcessorTime;
        private DateTime curTime;
        private TimeSpan curTotalProcessorTime;
        private long[] AffinityMasks;
        private int coresPerCCX = 0;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public Program()
        {
            // Determing CCX count by sniffing processor identifier (name) from OS
            String cpuId = System.Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            int family = int.Parse(cpuId.Substring(cpuId.IndexOf("Family") + 7, 2));
            int coreCount = Environment.ProcessorCount;
            if (family == 25)
            {
                // Zen 3: 6/8 Cores per CCX
                if (coreCount == 24)
                {
                    AffinityMasks = new long[] { 0xFFF, 0xFFF000 };
                    coresPerCCX = 6;
                }
                else if (coreCount == 32)
                {
                    AffinityMasks = new long[] { 0xFFFF, 0xFFFF0000 };
                    coresPerCCX = 8;
                }
            }
            else if (family == 23)
            {
                // Zen, Zen+, Zen 2: 3/4 Cores per CCX
                if (coreCount == 12)
                {
                    AffinityMasks = new long[] { 0x3F, 0xFC0 };
                    coresPerCCX = 3;
                }
                else if (coreCount == 16)
                {
                    AffinityMasks = new long[] { 0xFF, 0xFF00 };
                    coresPerCCX = 4;
                }
                else if (coreCount == 24)
                {
                    AffinityMasks = new long[] { 0x3F, 0xFC0, 0x3F000, 0xFC0000 };
                    coresPerCCX = 3;
                }
                else if (coreCount == 32)
                {
                    AffinityMasks = new long[] { 0xFF, 0xFF00, 0xFF0000, 0xFF000000 };
                    coresPerCCX = 4;
                }
            }
        }

        public int getCCXCount()
        {
            return AffinityMasks != null ? AffinityMasks.Length : 0;
        }

        public int getCoresPerCCX()
        {
            return coresPerCCX;
        }

        private static Process getForegroundProcess()
        {
            uint processID = 0;
            IntPtr hWnd = GetForegroundWindow(); // Get foreground window handle
            if (hWnd == null)
            {
                return null;
            }
            uint threadID = GetWindowThreadProcessId(hWnd, out processID); // Get PID from window handle
            if (processID == 0)
            {
                return null;
            }
            Process fgProc = Process.GetProcessById(Convert.ToInt32(processID)); // Get it as a C# obj.
                                                                                 // NOTE: In some rare cases ProcessID will be NULL. Handle this how you want. 
            return fgProc;
        }

        public void UpdateProcessAffinity(int mode)
        {
            Process proc = getForegroundProcess();
            if (proc == null)
            {
                return;
            }
            try
            {
                if (m_procId != proc.Id)
                {
                    if (m_proc != null && !m_proc.HasExited)
                    {
                        // Reset last process mask
                        foreach (ProcessThread t in proc.Threads)
                        {
                            if (t.ThreadState == System.Diagnostics.ThreadState.Running)
                            {
                                t.ProcessorAffinity = (IntPtr)0xFFFFFF;
                            }
                        }
                        Console.WriteLine("Restore affinity: " + m_proc.ProcessName);
                    }
                    m_proc = proc;
                    m_procId = proc.Id;
                    m_affinity = 0x7FFFFFFF;
                    lastTime = DateTime.Now;
                    if (proc.HasExited)
                    {
                        return;
                    }
                    lastTotalProcessorTime = proc.TotalProcessorTime;
                    return;
                }
                long AffinityMask = 0;
                curTime = DateTime.Now;
                if (proc.HasExited)
                {
                    return;
                }
                curTotalProcessorTime = proc.TotalProcessorTime;

                double CPUUsage = (curTotalProcessorTime.TotalMilliseconds - lastTotalProcessorTime.TotalMilliseconds) / curTime.Subtract(lastTime).TotalMilliseconds / Convert.ToDouble(Environment.ProcessorCount) * 100;

                lastTime = curTime;
                lastTotalProcessorTime = curTotalProcessorTime;
                double singleCCDUsage = (0.5 / AffinityMasks.Length) * 95;

                if (CPUUsage <= singleCCDUsage)
                {
                    // One CCX
                    AffinityMask = AffinityMasks[mode];
                }
                else
                {
                    // All CCXs
                    AffinityMask = 0xFFFFFFFF;
                }

                if (m_affinity != AffinityMask)
                {
                    m_affinity = AffinityMask;
                    if (!proc.HasExited)
                    {
                        foreach (ProcessThread t in proc.Threads)
                        {
                            if (t.ThreadState == System.Diagnostics.ThreadState.Running)
                            {
                                t.ProcessorAffinity = (IntPtr)AffinityMask;
                            }
                        }
                    }
                    Console.WriteLine("Process time: " + CPUUsage + ", affinity: " + m_affinity.ToString("X") + ", name: " + proc.ProcessName);
                }
            }
            catch (System.InvalidOperationException ex)
            {
                Console.WriteLine("Error accessing forground process");
            }
            catch (Win32Exception ex)
            {
                Console.WriteLine("Error accessing forground process");
            }
        }

        static void Main(string[] args)
        {
            int mode = (args.Length > 0 && (args[0] == "1" || args[0] == "2" || args[0] == "3" || args[0] == "4")) ? int.Parse(args[0]) : 1;
            Program prog = new Program();
            Console.WriteLine("Detected CCX Count: " + prog.getCCXCount());
            Console.WriteLine("Cores per CCX: " + prog.getCoresPerCCX());
            if (mode > prog.getCCXCount())
            {
                mode = prog.getCCXCount();
            }
            Console.WriteLine("Starting AutoCCX, preferred CCX: " + mode);
            while (true)
            {
                prog.UpdateProcessAffinity(mode - 1);
                Thread.Sleep(500);
            }
        }
    }
}
