using Eto.Drawing;
using Eto.Forms;
using System.Collections.ObjectModel;
using System;
using System.Diagnostics;
using System.IO;
using Resources = OpenTrace.Properties.Resources;
using NextTrace;
using System.Net;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Linq;
using Advexp;
using System.Collections.Generic;

namespace OpenTrace
{
    public partial class MainForm : Form
    {
        private ObservableCollection<TracerouteHop> tracerouteResultCollection = new ObservableCollection<TracerouteHop>();
        private static NextTraceWrapper CurrentInstance { get; set; }
        private static double gridSizePercentage = 0.5;
        private ComboBox HostInputBox;
        private GridView tracerouteGridView;
        private CheckBox MTRMode;
        private WebView mapWebView;
        private DropDown ResolvedIPSelection;
        private DropDown dataProviderSelection;
        private DropDown protocolSelection;
        private Button startTracerouteButton;
        private bool gridResizing = false;
        private bool appForceExiting = false;

        public MainForm()
        {
            Title = Resources.APPTITLE;
            MinimumSize = new Size(960, 720);

            // �����˵���
            var newWindowCommand = new Command { MenuText = Resources.NEW, ToolBarText = Resources.NEW_WINDOW_TEXT, Shortcut = Application.Instance.CommonModifier | Keys.N };
            newWindowCommand.Executed += (sender, e) =>
            {
                Process.Start(Process.GetCurrentProcess().MainModule.FileName);
            };

            var quitCommand = new Command { MenuText = Resources.QUIT, Shortcut = Application.Instance.CommonModifier | Keys.Q };
            quitCommand.Executed += (sender, e) => Application.Instance.Quit();

            var aboutCommand = new Command { MenuText = Resources.ABOUT };
            aboutCommand.Executed += (sender, e) => Process.Start(new ProcessStartInfo("https://github.com/Archeb/opentrace") { UseShellExecute = true });

            var preferenceCommand = new Command { MenuText = Resources.PREFERENCES };
            preferenceCommand.Executed += (sender, e) => new PreferencesDialog().ShowModal();

            // �����˵���
            Menu = new MenuBar
            {
                Items =
                {
                    new SubMenuItem { Text = Resources.FILE, Items = {
                            newWindowCommand,
                            preferenceCommand,
                            quitCommand
                        } },
                     new SubMenuItem { Text = Resources.HELP , Items = {
                             aboutCommand
                         } }
                }
            };

            // �����ؼ�
            HostInputBox = new ComboBox { Text = "" };
            HostInputBox.KeyDown += HostInputBox_KeyDown;
            HostInputBox.TextChanged += HostInputBox_TextChanged;
            if(UserSettings.traceHistory != null || UserSettings.traceHistory!= "")
            {
                foreach (string item in UserSettings.traceHistory.Split('\n'))
                {
                    if(item != "")
                    {
                        HostInputBox.Items.Add(item);
                    }
                }
            }

            MTRMode = new CheckBox { Text = Resources.MTR_MODE };
            MTRMode.CheckedChanged += MTRMode_CheckedChanged;

            ResolvedIPSelection = new DropDown { Visible = false };

            startTracerouteButton = new Button { Text = Resources.START };
            startTracerouteButton.Click += StartTracerouteButton_Click;

            protocolSelection = new DropDown
            {
                Items = {
                    new ListItem{Text = "ICMP" ,Key= ""},
                    new ListItem{Text = "TCP",Key = "-T" },
                    new ListItem{Text = "UDP",Key = "-U" },
                },
                SelectedIndex = 0,
                ToolTip = Resources.PROTOCOL_FOR_TRACEROUTING
            };
            dataProviderSelection = new DropDown
            {
                Items = {
                    new ListItem{Text = "LeoMoeAPI", Key= ""},
                    new ListItem{Text = "IPInfo", Key = "--data-provider IPInfo" },
                    new ListItem{Text = "IP.SB", Key = "--data-provider IP.SB" },
                    new ListItem{Text = "Ip2region", Key = "--data-provider Ip2region" },
                    new ListItem{Text = "IPInsight", Key = "--data-provider IPInsight" },
                    new ListItem{Text = "IPAPI.com", Key = "--data-provider IPAPI.com" },
                    new ListItem{Text = "IPInfoLocal", Key = "--data-provider IPInfoLocal" },
                    new ListItem{Text = "CHUNZHEN", Key = "--data-provider chunzhen"},
                    new ListItem{Text = Resources.DISABLE_IPGEO, Key = "--data-provider disable-geoip"}
                },
                SelectedIndex = 0,
                ToolTip = Resources.IP_GEO_DATA_PROVIDER
            };

            tracerouteGridView = new GridView { DataStore = tracerouteResultCollection };
            tracerouteGridView.MouseUp += Dragging_MouseUp;
            tracerouteGridView.SelectedRowsChanged += TracerouteGridView_SelectedRowsChanged;
            AddGridColumnsTraceroute();

            mapWebView = new WebView();
            switch (UserSettings.mapProvider)
            {
                case "baidu":
                    mapWebView.Url = new Uri("https://lbs.baidu.com/jsdemo/demo/webgl0_0.htm");
                    break;
                case "google":
                    mapWebView.Url = new Uri("https://geo-devrel-javascript-samples.web.app/samples/map-simple/app/dist/");
                    break;
            }
            mapWebView.DocumentLoaded += (sender6, e6) => {
                ResetMap();
            };

            // �󶨴����¼�
            SizeChanged += MainForm_SizeChanged;
            MouseDown += Dragging_MouseDown;
            MouseUp += Dragging_MouseUp;
            MouseMove += MainForm_MouseMove;

            // ʹ�� Table ���ִ���ҳ��
            var layout = new TableLayout
            {
                Padding = new Padding(10),
                Spacing = new Size(5, 5),
                Rows = {
                    new TableRow {
                        Cells = {
                        new TableLayout {
                            Spacing = new Size(10,10),
                            Rows =
                            {
                                new TableRow
                                {
                                    Cells =
                                    {
                                        new TableCell(HostInputBox,true),
                                        ResolvedIPSelection,
                                        MTRMode,
                                        protocolSelection,
                                        dataProviderSelection,
                                        startTracerouteButton
                                    }
                                }
                            }
                        }
                    }
                    },
                    new TableRow {
                        Cells = {tracerouteGridView}
                    },
                    new TableRow{
                        Cells = {mapWebView}
                    },
                }
            };
            Content = layout;

            HostInputBox.Focus(); // �Զ��۽������
        }

        private void HostInputBox_TextChanged(object sender, EventArgs e)
        {
            // ����ı����޸ģ������� DNS ����ѡ���
            if (ResolvedIPSelection.Visible)
            {
                ResolvedIPSelection.Items.Clear();
                ResolvedIPSelection.Visible = false;
            }
        }

        private void MTRMode_CheckedChanged(object sender, EventArgs e)
        {
            if ((bool)MTRMode.Checked)
            {
                AddGridColumnsMTR();
            }
            else
            {
                AddGridColumnsTraceroute();
            }
        }

        private void HostInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Keys.Enter)
            {
                if (CurrentInstance != null)  StopTraceroute();
                StartTracerouteButton_Click(sender, e);
            }
        }

        private void TracerouteGridView_SelectedRowsChanged(object sender, EventArgs e)
        {
            FocusMapPoint(tracerouteGridView.SelectedRow);
        }

        private void StartTracerouteButton_Click(object sender, EventArgs e)
        {
            if(protocolSelection.SelectedValue.ToString() != "ICMP" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                MessageBox.Show(Resources.WINDOWS_TCP_UDP_UNSUPPORTED);
                return;
            }
            if (CurrentInstance != null)
            {
                StopTraceroute();
                return;
            }
            tracerouteResultCollection.Clear(); // ���ԭ��GridView
            ResetMap(); // ���õ�ͼ
            Title = Resources.APPTITLE;
            NextTraceWrapper instance;
            try
            {
                instance = new NextTraceWrapper();
            }
            catch (FileNotFoundException)
            {
                // δ����Ĭ����ѰĿ¼���ҵ�NextTrace��ѯ���Ƿ����� NextTrace
                DialogResult dr = MessageBox.Show(Resources.MISSING_COMP_TEXT,
                     Resources.MISSING_COMP, MessageBoxButtons.YesNo);
                if (dr == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo("https://mtr.moe/") { UseShellExecute = true });
                }
                return;
            }
            catch(IOException exception)
            {
                // δ����ָ����λ���ҵ� NextTrace
                MessageBox.Show(string.Format(Resources.MISSING_SPECIFIED_COMP, exception.Message), Resources.MISSING_COMP);
                return;
            }

            // �����ı�������
            string readyToUseIP;
            if (ResolvedIPSelection.Visible == true && ResolvedIPSelection.SelectedIndex != 0)
            {
                readyToUseIP = ResolvedIPSelection.SelectedKey;
                Title = Resources.APPTITLE + ": " + HostInputBox.Text + " (" + readyToUseIP + ")";
            }
            else if(ResolvedIPSelection.Visible == true && ResolvedIPSelection.SelectedIndex == 0)
            {
                MessageBox.Show(Resources.SELECT_IP_MSGBOX);
                return;
            }
            else
            {
                ResolvedIPSelection.Visible = false; // ���� IP ѡ���
                IPAddress userInputAddress;
                if (IPAddress.TryParse(HostInputBox.Text, out userInputAddress))
                {
                    // �ǺϷ��� IPv4 / IPv6���ѳ�������IP�Ż��ı���
                    HostInputBox.Text = userInputAddress.ToString();
                    readyToUseIP = userInputAddress.ToString();
                    Title = Resources.APPTITLE + ": " + readyToUseIP;
                }
                else
                {
                    try
                    {
                        Uri uri;
                        if (Uri.TryCreate(HostInputBox.Text, UriKind.Absolute, out uri) && uri.Host != "")
                        {
                            // �ǺϷ��� URL
                            HostInputBox.Text = uri.Host;
                        }
                        // ��Ҫ��������
                        Title = Resources.APPTITLE + ": " + HostInputBox.Text;
                        IPAddress[] resolvedAddresses = Dns.GetHostAddresses(HostInputBox.Text);
                        if (resolvedAddresses.Length > 1)
                        {
                            ResolvedIPSelection.Items.Clear();
                            ResolvedIPSelection.Items.Add(Resources.SELECT_IP_DROPDOWN);
                            foreach (IPAddress resolvedAddress in resolvedAddresses)
                            {
                                ResolvedIPSelection.Items.Add(resolvedAddress.ToString());
                            }
                            ResolvedIPSelection.SelectedIndex = 0;
                            ResolvedIPSelection.Visible = true;
                            return;
                        }
                        else
                        {
                            readyToUseIP = resolvedAddresses[0].ToString();
                            Title = Resources.APPTITLE + ": " + HostInputBox.Text + " (" + readyToUseIP + ")";
                        }
                    }
                    catch (System.Net.Sockets.SocketException)
                    {
                        MessageBox.Show(string.Format(Resources.NAME_NOT_RESOLVED, HostInputBox.Text), MessageBoxType.Warning);
                        Title = Resources.APPTITLE;
                        return;
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(exception.Message, MessageBoxType.Error);
                        Title = Resources.APPTITLE;
                        return;
                    }
                }
            }

            string newText = HostInputBox.Text;
            // �����ظ���¼
            IList<IListItem> clone = HostInputBox.Items.ToList();
            foreach (var toRemove in clone.Where(s => s.Text == newText))
            {
                HostInputBox.Items.Remove(toRemove); // ��֪��Ϊʲô����� ComboBox �� Item ���ͬ���ı��������һ�����
            }
            HostInputBox.Text = newText; // ���Ե����������·Ż�ȥ
            HostInputBox.Items.Insert(0, new ListItem { Text = newText });
            while (HostInputBox.Items.Count > 20) // ����20�����ϼ�¼
            {
                HostInputBox.Items.RemoveAt(HostInputBox.Items.Count - 1);
            }
            UserSettings.traceHistory = String.Join("\n", HostInputBox.Items.Select(item => item.Text));
            UserSettings.SaveSettings();
            CurrentInstance = instance;
            startTracerouteButton.Text = Resources.STOP;
            int errorOutputCount = 0;

            ExceptionalOutputForm exceptionalOutputForm = new ExceptionalOutputForm();

            // ����NextTraceʵ�����صĽ��
            instance.Output.CollectionChanged += (sender1, e1) =>
            {
                if (e1.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    Application.Instance.InvokeAsync(() =>
                    {
                        int HopNo = int.Parse(((TracerouteResult)e1.NewItems[0]).No);
                        if (HopNo > tracerouteResultCollection.Count)
                        {
                            // ��������µ���
                            tracerouteResultCollection.Add(new TracerouteHop((TracerouteResult)e1.NewItems[0]));
                            UpdateMap((TracerouteResult)e1.NewItems[0]);
                            tracerouteGridView.ScrollToRow(tracerouteResultCollection.Count - 1);
                        } else {
                            // �޸����е���
                            tracerouteResultCollection[HopNo - 1].HopData.Add((TracerouteResult)e1.NewItems[0]);
                            tracerouteGridView.ReloadData(HopNo - 1);
                        }
                    });
                }
            };
            instance.ExceptionalOutput += (object sender2, ExceptionalOutputEventArgs e2) =>
            {
                Application.Instance.InvokeAsync(() =>
                {
                    exceptionalOutputForm.Show();
                    if (!exceptionalOutputForm.Visible)
                    {
                        exceptionalOutputForm.Visible = true;
                    }
                    if(errorOutputCount < 100)
                    {
                        errorOutputCount++;
                    }
                    else
                    {
                        StopTraceroute(); // ����������࣬ǿ�ƽ���
                    }
                    exceptionalOutputForm.AppendOutput(e2.Output);
                });
            };
            instance.AppQuit += (object sender3, AppQuitEventArgs e3) =>
            {
                Application.Instance.InvokeAsync(() =>
                {
                    if (appForceExiting != true)
                    {
                        // ��������
                        startTracerouteButton.Text = Resources.START;
                        CurrentInstance = null;
                        if (e3.ExitCode != 0)
                        {
                            MessageBox.Show(Resources.EXCEPTIONAL_EXIT_MSG + e3.ExitCode, MessageBoxType.Warning);
                        }
                    }
                    else
                    {
                        // ǿ�ƽ���
                        appForceExiting = false;
                    }
                });
            };
            instance.Run(readyToUseIP, (bool)MTRMode.Checked, dataProviderSelection.SelectedKey, protocolSelection.SelectedKey);
            
        }
        private void StopTraceroute()
        {
            if(CurrentInstance != null)
            {
                appForceExiting = true;
                CurrentInstance.Kill();
                startTracerouteButton.Text = Resources.START;
                CurrentInstance = null;
            }
        }

        /*
         * ������ק���� GridView ��С
         */
        private void Dragging_MouseUp(object sender, MouseEventArgs e)
        {
            gridResizing = false;
            mapWebView.Enabled = true;
        }

        private void Dragging_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Location.Y >= tracerouteGridView.Bounds.Bottom + 15 && e.Location.Y <= tracerouteGridView.Bounds.Bottom + 20)
            {
                gridResizing = true;
                mapWebView.Enabled = false;
            }

        }

        private void MainForm_MouseMove(object sender, MouseEventArgs e)
        {
            // �������ָ��
            if (e.Location.Y >= tracerouteGridView.Bounds.Bottom + 15 && e.Location.Y <= tracerouteGridView.Bounds.Bottom + 20)
            {
                this.Cursor = Cursors.SizeBottom;
            }
            else
            {
                this.Cursor = Cursors.Default;
            }

            if (e.Buttons == MouseButtons.Primary && gridResizing)
            {
                if ((int)e.Location.Y > (tracerouteGridView.Bounds.Top + 100)) // ��С����Ϊ100px
                {

                    tracerouteGridView.Height = (int)e.Location.Y - tracerouteGridView.Bounds.Top - 15;
                    gridSizePercentage = (double)tracerouteGridView.Height / (Height - 75); // �������
                }
            }
        }

        private void MainForm_SizeChanged(object sender, EventArgs e)
        {
            int gridHeight;
            int totalHeight = this.Height - 75; // ��ȥ�߾��������ı����75px
            gridHeight = (int)(totalHeight * gridSizePercentage);
            tracerouteGridView.Height = gridHeight; // ��������ԭ�߶�
        }
        private void UpdateMap(TracerouteResult result)
        {
            // �� Result ת��Ϊ JSON
            string resultJson = JsonConvert.SerializeObject(result);
            // ͨ�� ExecuteScriptAsync �ѽ������ȥ
            mapWebView.ExecuteScriptAsync(@"window.opentrace.addHop(`" + resultJson + "`);");
        }
        private void FocusMapPoint(int hopNo)
        {
            mapWebView.ExecuteScriptAsync(@"window.opentrace.focusHop(" + hopNo + ");");
        }
        private void ResetMap()
        {
            // ���û��߳�ʼ����ͼ
            switch (mapWebView.Url.Host)
            {
                case "geo-devrel-javascript-samples.web.app":
                    mapWebView.ExecuteScriptAsync(OpenTrace.Properties.Resources.googleMap);
                    break;
                case "lbs.baidu.com":
                    mapWebView.ExecuteScriptAsync(OpenTrace.Properties.Resources.baiduMap);
                    break;
            }
            mapWebView.ExecuteScriptAsync("window.opentrace.reset(" + UserSettings.hideMapPopup.ToString().ToLower() + ")");
        }
        private void AddGridColumnsTraceroute()
        {
            tracerouteGridView.Columns.Clear();
            // ָ����λ����Դ
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.No) },
                HeaderText = "#"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.IP) },
                HeaderText = "IP"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Time) },
                HeaderText = Resources.TIME_MS
            });
            // �ϲ�λ�ú���Ӫ��
            if (UserSettings.combineGeoOrg == true)
            {
                tracerouteGridView.Columns.Add(new GridColumn
                {
                    DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.GeolocationAndOrganization) },
                    HeaderText = Resources.GEOLOCATION
                });
            }
            else
            {
                tracerouteGridView.Columns.Add(new GridColumn
                {
                    DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Geolocation) },
                    HeaderText = Resources.GEOLOCATION
                });
                tracerouteGridView.Columns.Add(new GridColumn
                {
                    DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Organization) },
                    HeaderText = Resources.ORGANIZATION
                });
            }
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.AS) },
                HeaderText = "AS"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Hostname) },
                HeaderText = Resources.HOSTNAME
            });
        }
        private void AddGridColumnsMTR()
        {
            tracerouteGridView.Columns.Clear();
            // ָ����λ����Դ
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.No) },
                HeaderText = "#"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.IP) },
                HeaderText = "IP"
            });
            // �ϲ�λ�ú���Ӫ��
            if (UserSettings.combineGeoOrg == true)
            {
                tracerouteGridView.Columns.Add(new GridColumn
                {
                    DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.GeolocationAndOrganization) },
                    HeaderText = Resources.GEOLOCATION
                });
            }
            else
            {
                tracerouteGridView.Columns.Add(new GridColumn
                {
                    DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Geolocation) },
                    HeaderText = Resources.GEOLOCATION
                });
                tracerouteGridView.Columns.Add(new GridColumn
                {
                    DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Organization) },
                    HeaderText = Resources.ORGANIZATION
                });
            }
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Loss.ToString()) },
                HeaderText = Resources.LOSS
            });

            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Sent.ToString()) },
                HeaderText = Resources.SENT
            });

            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Recv.ToString()) },
                HeaderText = Resources.RECV
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Last.ToString()) },
                HeaderText = Resources.LAST
            });

            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Worst.ToString()) },
                HeaderText = Resources.WORST
            });

            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Best.ToString()) },
                HeaderText = Resources.BEST
            });

            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Average.ToString("0.##")) },
                HeaderText = Resources.AVRG
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.StandardDeviation.ToString("0.##")) },
                HeaderText = Resources.STDEV
            });
            /* TODO
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => "TODO") },
                HeaderText = Resources.HISTORY
            }); */
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.AS) },
                HeaderText = "AS"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Hostname) },
                HeaderText = Resources.HOSTNAME
            });
        }
    }
}
