using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class SborishcheUpdater : Form {
  readonly JavaScriptSerializer js = new JavaScriptSerializer();
  readonly Color dark = Color.FromArgb(25,22,28), gold=Color.FromArgb(225,177,102), pale=Color.FromArgb(243,234,224);
  TextBox host, pin, selected, apk; ComboBox ips; Label status; RichTextBox log;
  Button connect, send, rollback, start, stop, configure, sendApk;
  string baseUrl="", token="", bundlePath="", apkPath="", version="", fileName="";
  int bundleCode; byte[] servedBytes; TcpListener tcp; volatile bool serving;
  public SborishcheUpdater() {
    Text="СБОРИЩЕ — Центр обновлений"; ClientSize=new Size(850,690); MinimumSize=new Size(850,690);
    StartPosition=FormStartPosition.CenterScreen; Font=new Font("Segoe UI",10); BackColor=dark; ForeColor=pale;
    AutoScroll=true; ServicePointManager.Expect100Continue=false; MakeUi();
    FormClosing+=(s,e)=>StopSharing();
  }
  Label Txt(string s,int x,int y,int w,int h,bool title=false) {
    var l=new Label{Text=s,Location=new Point(x,y),Size=new Size(w,h),ForeColor=title?gold:pale,Font=new Font("Segoe UI",title?12:10,title?FontStyle.Bold:FontStyle.Regular)};
    Controls.Add(l);return l;
  }
  TextBox Input(int x,int y,int w) {
    var t=new TextBox{Location=new Point(x,y),Size=new Size(w,32),Font=new Font("Segoe UI",11),BackColor=Color.FromArgb(42,36,46),ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle};
    Controls.Add(t);return t;
  }
  Button Btn(string s,int x,int y,int w,bool primary=false) {
    var b=new Button{Text=s,Location=new Point(x,y),Size=new Size(w,35),FlatStyle=FlatStyle.Flat,BackColor=primary?gold:Color.FromArgb(84,69,82),ForeColor=primary?Color.Black:Color.White,Cursor=Cursors.Hand};
    b.FlatAppearance.BorderSize=0;Controls.Add(b);return b;
  }
  void MakeUi() {
    Txt("СБОРИЩЕ",20,12,600,39,true);Txt("Обновления телефона через домашний Wi-Fi · Cardinalich Software",20,48,795,28);
    Txt("1. Подключение к телефону",20,86,600,27,true);
    host=Input(20,120,404);host.Text="http://192.168.1.50:3217";
    pin=Input(436,120,145);pin.PasswordChar='●';pin.MaxLength=8;
    connect=Btn("Подключиться",595,117,235,true);
    status=Txt("Адрес телефона · PIN из «Обновления по Wi-Fi»",20,158,795,25);
    Txt("2. Отправить подписанный игровой ZIP",20,196,600,26,true);
    selected=Input(20,227,557);selected.ReadOnly=true;
    Btn("Выбрать ZIP",590,225,240).Click+=(s,e)=>{using(var f=new OpenFileDialog{Filter="Подписанные игровые пакеты (*.zip)|*.zip"})if(f.ShowDialog()==DialogResult.OK){bundlePath=f.FileName;selected.Text=bundlePath;Note("Выбран "+Path.GetFileName(bundlePath));}};
    send=Btn("Установить на телефоне",20,272,299,true);
    rollback=Btn("Откатить версию",333,272,235);
    Btn("Обновить статус",581,272,249).Click+=async(s,e)=>await Run(null,()=>{var d=Call("GET","/updates/info");return "Android "+Field(d,"hostVersion")+", комплект "+Field(d,"activeBundle")+(Field(d,"busy")=="True"?" (идёт раунд)":"");});
    Txt("3. Раздавать обновления автоматически",20,321,740,27,true);
    Txt("ПК держит подписанный ZIP, телефон проверяет его раз в 3 минуты (если компьютер включён).",20,354,812,25);
    ips=new ComboBox{Location=new Point(20,387),Size=new Size(185,29),DropDownStyle=ComboBoxStyle.DropDownList,Font=new Font("Segoe UI",10)};
    Controls.Add(ips);foreach(var a in LocalIps())ips.Items.Add(a);if(ips.Items.Count==0)ips.Items.Add("127.0.0.1");ips.SelectedIndex=0;
    start=Btn("Начать раздачу",220,383,187,true);stop=Btn("Остановить",419,383,172);stop.Enabled=false;
    configure=Btn("Привязать телефон к ПК",603,383,227);
    Txt("4. Обновить Android APK (только для изменений нативного приложения)",20,434,795,26,true);
    apk=Input(20,469,515);apk.ReadOnly=true;
    Btn("Выбрать APK",549,467,132).Click+=(s,e)=>{using(var f=new OpenFileDialog{Filter="Android приложение (*.apk)|*.apk"})if(f.ShowDialog()==DialogResult.OK){apkPath=f.FileName;apk.Text=apkPath;}};
    sendApk=Btn("Передать APK",695,467,135);
    Txt("Журнал операций",20,522,450,26,true);
    log=new RichTextBox{Location=new Point(20,551),Size=new Size(810,112),ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Color.FromArgb(39,33,43),ForeColor=gold,Font=new Font("Consolas",9)};
    Controls.Add(log);Note("Установите Android HOST v1.1.0, откройте «Обновления по Wi-Fi», введите адрес и PIN.");
    connect.Click+=async(s,e)=>{
      string address=host.Text.Trim(),secret=pin.Text.Trim();
      try {baseUrl=Normalize(address);}catch(Exception ex){Note(ex.Message);return;}
      if(secret.Length!=8 || secret.Any(c=>c<'0'||c>'9')){Note("PIN должен содержать 8 цифр");return;}
      await Run(connect,()=>{var d=Call("GET","/updates/info");var a=Call("POST","/updates/login",Encoding.UTF8.GetBytes(js.Serialize(new {pin=secret})),"application/json");token=Field(a,"token");return "Телефон подключён: Android "+Field(d,"hostVersion")+", комплект "+Field(d,"activeBundle");});
    };
    send.Click+=async(s,e)=>{
      if(!Ready()||String.IsNullOrEmpty(bundlePath)||!File.Exists(bundlePath)){Note("Выберите ZIP и подключитесь");return;}
      string v;int code;try{Manifest(bundlePath,out v,out code);}catch(Exception ex){Note("Ошибка ZIP: "+ex.Message);return;}
      if(MessageBox.Show("Установить игровой комплект "+v+"? Активный раунд должен быть завершён.","СБОРИЩЕ",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
      string path=bundlePath;
      await Run(send,()=>{byte[] b=File.ReadAllBytes(path);var d=Call("POST","/updates/install",b,"application/zip");return "Установлен игровой комплект №"+Field(d,"activeBundle");});
    };
    rollback.Click+=async(s,e)=>{if(!Ready())return;
      if(MessageBox.Show("Откатить игровые файлы? Сохранения и музыка не удаляются.","Откат",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
      await Run(rollback,()=>{var d=Call("POST","/updates/rollback",Encoding.UTF8.GetBytes("{}"),"application/json");return "Откат выполнен, комплект №"+Field(d,"activeBundle");});
    };
    start.Click+=(s,e)=>{try{string v;int c;Manifest(bundlePath,out v,out c);servedBytes=File.ReadAllBytes(bundlePath);if(servedBytes.Length>17*1024*1024)throw new Exception("ZIP слишком большой");
       version=v;bundleCode=c;fileName="SBORISHCHE_UPDATE_"+v+".zip";tcp=new TcpListener(IPAddress.Any,8765);tcp.Start();serving=true;
       Thread t=new Thread(Serve){IsBackground=true};t.Start();start.Enabled=false;stop.Enabled=true;Note("Раздача запущена: http://"+ips.Text+":8765/latest.json");
      }catch(Exception ex){Note("Ошибка раздачи: "+ex.Message);}};
    stop.Click+=(s,e)=>{StopSharing();start.Enabled=true;stop.Enabled=false;Note("Раздача остановлена");};
    configure.Click+=async(s,e)=>{if(!Ready()||!serving){Note("Сперва подключитесь и запустите раздачу");return;}
      string url="http://"+ips.Text+":8765/latest.json";
      await Run(configure,()=>{Call("POST","/updates/config",Encoding.UTF8.GetBytes(js.Serialize(new {url=url})),"application/json");var d=Call("POST","/updates/check",Encoding.UTF8.GetBytes("{}"),"application/json");return "Телефон теперь проверяет "+url+"; поиск выполнен";});
    };
    sendApk.Click+=async(s,e)=>{if(!Ready()||String.IsNullOrEmpty(apkPath)||!File.Exists(apkPath)){Note("Выберите APK и подключитесь");return;}
      if(MessageBox.Show("Телефон откроет системный установщик. Подтвердить передачу APK?","Android",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
      string p=apkPath;await Run(sendApk,()=>{byte[] b=File.ReadAllBytes(p);if(b.Length>45*1024*1024)throw new Exception("APK больше 45 МБ");var d=Call("POST","/updates/native-apk",b,"application/vnd.android.package-archive");return "APK проверен. На телефоне подтвердите установку. "+Field(d,"notice");});
    };
  }
  void Note(string s){if(IsDisposed)return;if(InvokeRequired){BeginInvoke(new Action<string>(Note),s);return;}log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+s+Environment.NewLine);log.ScrollToCaret();status.Text=s;}
  async Task Run(Button b,Func<string> operation){if(b!=null)b.Enabled=false;try{string result=await Task.Run(operation);Note(result);}catch(Exception ex){Note("ОШИБКА: "+ex.Message);}finally{if(b!=null&&!IsDisposed)b.Enabled=true;}}
  bool Ready(){if(String.IsNullOrEmpty(baseUrl)||String.IsNullOrEmpty(token)){Note("Сначала подключитесь к телефону по PIN");return false;}return true;}
  static string Normalize(string input){if(!input.StartsWith("http://"))input="http://"+input;Uri u=new Uri(input);IPAddress ip;
    if(u.Scheme!="http" || !IPAddress.TryParse(u.Host,out ip)||ip.AddressFamily!=AddressFamily.InterNetwork)throw new Exception("Укажите локальный IPv4-адрес телефона");
    byte[] b=ip.GetAddressBytes();if(!(b[0]==10 || (b[0]==172&&b[1]>=16&&b[1]<=31)||(b[0]==192&&b[1]==168)||b[0]==127))throw new Exception("Нужен IP из локальной сети");
    return "http://"+ip+":"+(u.IsDefaultPort?3217:u.Port);
  }
  Dictionary<string,object> Call(string method,string path,byte[] body=null,string type=null){
    if(String.IsNullOrEmpty(baseUrl))throw new Exception("Адрес телефона не задан");
    var r=(HttpWebRequest)WebRequest.Create(baseUrl+path);r.Method=method;r.Timeout=25000;r.ReadWriteTimeout=25000;r.KeepAlive=false;r.Proxy=null;
    if(!String.IsNullOrEmpty(token))r.Headers["X-Sbor-Admin-Token"]=token;
    if(body!=null){r.ContentType=type;r.ContentLength=body.Length;using(var s=r.GetRequestStream())s.Write(body,0,body.Length);}
    try{using(var resp=(HttpWebResponse)r.GetResponse())using(var sr=new StreamReader(resp.GetResponseStream(),Encoding.UTF8))return Parse(sr.ReadToEnd());}
    catch(WebException e){if(e.Response!=null)using(var sr=new StreamReader(e.Response.GetResponseStream(),Encoding.UTF8)){string data=sr.ReadToEnd();try{return RaiseServerError(data);}catch(ArgumentException){throw new Exception(data);}}throw new Exception("Нет соединения с телефоном: "+e.Message);}
  }
  Dictionary<string,object> RaiseServerError(string body){var d=Parse(body);throw new Exception(Field(d,"error"));}
  Dictionary<string,object> Parse(string s){var d=js.DeserializeObject(s) as Dictionary<string,object>;if(d==null)throw new ArgumentException("Invalid JSON");return d;}
  static string Field(Dictionary<string,object> d,string k){return d.ContainsKey(k)?Convert.ToString(d[k]):"";}
  void Manifest(string path,out string v,out int code){if(String.IsNullOrEmpty(path)||!File.Exists(path))throw new Exception("Сначала выберите ZIP");
    using(var z=ZipFile.OpenRead(path)){var m=z.GetEntry("manifest.json");if(m==null||z.GetEntry("manifest.sig")==null)throw new Exception("Это не подписанный игровой ZIP");
      using(var sr=new StreamReader(m.Open(),Encoding.UTF8)){var d=Parse(sr.ReadToEnd());v=Field(d,"version");if(!Int32.TryParse(Field(d,"bundleCode"),out code)||code<=0||v.Length==0)throw new Exception("Неверный manifest.json");}
    }
  }
  static IEnumerable<string> LocalIps(){var xs=new List<string>();foreach(var a in Dns.GetHostAddresses(Dns.GetHostName()))if(a.AddressFamily==AddressFamily.InterNetwork){byte[] b=a.GetAddressBytes();if(b[0]==10||(b[0]==172&&b[1]>=16&&b[1]<=31)||(b[0]==192&&b[1]==168))xs.Add(a.ToString());}return xs.Distinct();}
  void StopSharing(){serving=false;try{if(tcp!=null)tcp.Stop();}catch{}}
  void Serve(){while(serving)try{TcpClient client=tcp.AcceptTcpClient();ThreadPool.QueueUserWorkItem(o=>ServeOne(client));}catch(SocketException){}catch(ObjectDisposedException){}}
  void ServeOne(TcpClient c){using(c)try{c.ReceiveTimeout=5000;using(var s=c.GetStream()){
      var r=new StreamReader(s,Encoding.ASCII,false,1024,true);string first=r.ReadLine();if(first==null)return;
      for(int i=0;i<60;i++){string line=r.ReadLine();if(String.IsNullOrEmpty(line))break;}
      string[] p=first.Split(' ');if(p.Length<2||p[0]!="GET")return;
      var remote=((IPEndPoint)c.Client.RemoteEndPoint).Address.GetAddressBytes();
      if(remote.Length!=4 || !(remote[0]==10||remote[0]==127||(remote[0]==192&&remote[1]==168)||(remote[0]==172&&remote[1]>=16&&remote[1]<=31)))return;
      byte[] data;string typ;
      if(p[1].StartsWith("/latest.json")){string ip=((IPEndPoint)c.Client.LocalEndPoint).Address.ToString();data=Encoding.UTF8.GetBytes(js.Serialize(new {bundleCode=bundleCode,version=version,packageUrl="http://"+ip+":8765/"+fileName}));typ="application/json";}
      else if(p[1]=="/"+fileName){data=servedBytes;typ="application/zip";}else{return;}
      byte[] h=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: "+typ+"\r\nContent-Length: "+data.Length+"\r\nConnection: close\r\n\r\n");
      s.Write(h,0,h.Length);s.Write(data,0,data.Length);s.Flush();
    }}catch(Exception ex){Note("Раздача: "+ex.Message);}
  }
  [STAThread] static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new SborishcheUpdater());}
}