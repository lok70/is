using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CLIPSNET;
using Newtonsoft.Json;
using System.Globalization; // Для корректной точки в числах

namespace ClipsFormsExample
{
    // === МОДЕЛИ ДАННЫХ ===
    public class FactDefinition { public string id { get; set; } public string text { get; set; } }

    public class DialogVariant { public string text { get; set; } public string react { get; set; } }

    public class DialogDefinition
    {
        public string id { get; set; }
        public List<string> conditions { get; set; }
        public string target { get; set; }
        public string message { get; set; }
        public List<DialogVariant> variants { get; set; }
    }

    public class FactList { public List<FactDefinition> facts { get; set; } }

    public class RuleDefinition
    {
        public string id { get; set; }
        public List<string> conditions { get; set; }
        public string conclusion { get; set; }
        public double cf { get; set; } = 1.0;
    }

    public class RuleList { public List<RuleDefinition> rules { get; set; } }
    public class DialogList { public List<DialogDefinition> dialogs { get; set; } }

    public partial class ClipsFormsExample : Form
    {
        private CLIPSNET.Environment clips = new CLIPSNET.Environment();
        private List<FactDefinition> allFacts;
        private List<RuleDefinition> allRules;
        private List<DialogDefinition> allDialogs;

        public ClipsFormsExample()
        {
            InitializeComponent();
            SetupGrid();
            LoadKnowledgeBase();
        }

        private void SetupGrid()
        {
            factsGrid.Columns.Clear();

            var checkCol = new DataGridViewCheckBoxColumn();
            checkCol.HeaderText = "Выбор";
            checkCol.Name = "Selected";
            checkCol.Width = 50;
            factsGrid.Columns.Add(checkCol);

            var idCol = new DataGridViewTextBoxColumn();
            idCol.HeaderText = "ID";
            idCol.Name = "ID";
            idCol.ReadOnly = true;
            idCol.Width = 60;
            factsGrid.Columns.Add(idCol);

            var textCol = new DataGridViewTextBoxColumn();
            textCol.HeaderText = "Симптом";
            textCol.Name = "Text";
            textCol.ReadOnly = true;
            textCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            factsGrid.Columns.Add(textCol);

            var cfCol = new DataGridViewTextBoxColumn();
            cfCol.HeaderText = "Уверенность (%)";
            cfCol.Name = "CF";
            cfCol.Width = 80;
            factsGrid.Columns.Add(cfCol);
        }

        private void LoadKnowledgeBase()
        {
            try
            {
                if (File.Exists("facts.json"))
                    allFacts = JsonConvert.DeserializeObject<FactList>(File.ReadAllText("facts.json", Encoding.UTF8)).facts;

                if (File.Exists("rules.json"))
                    allRules = JsonConvert.DeserializeObject<RuleList>(File.ReadAllText("rules.json", Encoding.UTF8)).rules;

                if (File.Exists("dialogs.json"))
                    allDialogs = JsonConvert.DeserializeObject<DialogList>(File.ReadAllText("dialogs.json", Encoding.UTF8)).dialogs;
                else
                    allDialogs = new List<DialogDefinition>();

                if (allFacts != null)
                {
                    factsGrid.Rows.Clear();
                    foreach (var fact in allFacts)
                        if (string.Compare(fact.id, "F046") < 0)
                            factsGrid.Rows.Add(false, fact.id, fact.text, "100");
                }
            }
            catch (Exception ex) { MessageBox.Show("Ошибка загрузки: " + ex.Message); }
        }

        private void nextBtn_Click(object sender, EventArgs e)
        {
            outputBox.Clear();
            buttonsPanel.Controls.Clear();
            clips.Clear();

            string generatedClips = GenerateClipsCode();
            clips.LoadFromString(generatedClips);

            if (clipsOpenFileDialog.FileNames.Length > 0)
                foreach (string file in clipsOpenFileDialog.FileNames) clips.Load(file);

            clips.Reset();

            outputBox.AppendText("Исходные данные:\r\n");

            foreach (DataGridViewRow row in factsGrid.Rows)
            {
                if (Convert.ToBoolean(row.Cells["Selected"].Value))
                {
                    string id = row.Cells["ID"].Value.ToString();
                    string cfInput = row.Cells["CF"].Value?.ToString();

                    double userCf = 1.0;
                    if (double.TryParse(cfInput, out double parsedVal))
                        userCf = parsedVal / 100.0;

                    if (userCf > 1.0) userCf = 1.0;
                    if (userCf < 0.0) userCf = 0.0;

                    string cfStr = userCf.ToString("0.00", CultureInfo.InvariantCulture);

                    clips.Eval($"(assert (fact (id {id}) (cf {cfStr})))");
                    outputBox.AppendText($"- {id} (CF: {userCf * 100:F0}%)\r\n");
                }
            }
            outputBox.AppendText("\r\n--- Старт анализа (с учетом CF) ---\r\n");
            RunInferenceLoop();
        }

        private void RunInferenceLoop()
        {
            clips.Run();
            HandleInteractiveDialogue();
        }

        private void HandleInteractiveDialogue()
        {
            var evalStr = "(find-fact ((?f ioproxy)) TRUE)";
            var result = clips.Eval(evalStr);

            if (result is MultifieldValue mf && mf.Count > 0)
            {
                FactAddressValue fv = (FactAddressValue)mf[0];
                string questionId = "";

                try { questionId = ((LexemeValue)fv["question-id"]).Value; } catch { }

                var dialogData = allDialogs.FirstOrDefault(d => d.id == questionId);

                if (dialogData != null)
                {
                    outputBox.AppendText($"\r\n[СИСТЕМА]: {dialogData.message}\r\n");
                    buttonsPanel.Controls.Clear();

                    foreach (var variant in dialogData.variants)
                    {
                        Button btn = new Button { Text = variant.text, AutoSize = true, Padding = new Padding(5) };
                        btn.Tag = new { ReactId = variant.react, QId = questionId };
                        btn.Click += OnDialogButtonClick;
                        buttonsPanel.Controls.Add(btn);
                    }
                }
            }
            else
            {
                ShowConclusions();
            }
        }

        private void OnDialogButtonClick(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            dynamic tag = btn.Tag;
            string reactId = tag.ReactId;
            string questionId = tag.QId;

            outputBox.AppendText($"[ВЫ]: {btn.Text}\r\n");
            buttonsPanel.Controls.Clear();

            if (!string.IsNullOrEmpty(questionId))
                clips.Eval($"(do-for-all-facts ((?f ioproxy)) (eq ?f:question-id {questionId}) (retract ?f))");

            if (!string.IsNullOrEmpty(reactId) && reactId != "none" && reactId != "nil")
            {
                clips.Eval($"(assert (fact (id {reactId}) (cf 1.0)))");
                outputBox.AppendText($"-> Принят факт: {reactId}\r\n");
            }

            RunInferenceLoop();
        }

        // === ГЕНЕРАТОР CLIPS-КОДА ===
        private string GenerateClipsCode()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("(deftemplate fact (slot id) (slot text) (slot cf (type FLOAT) (default 0.0)))");
            sb.AppendLine("(deftemplate ioproxy (slot question-id) (multislot messages) (multislot answers-text) (multislot answers-react))");

            // Правила диалогов
            if (allDialogs != null)
            {
                foreach (var diag in allDialogs)
                {
                    sb.AppendLine($"(defrule ask-{diag.id}");
                    foreach (var cond in diag.conditions) sb.AppendLine($"   (fact (id {cond}) (cf ?cf_{cond}&:(> ?cf_{cond} 0.2)))");

                    sb.AppendLine($"   (not (fact (id {diag.target})))");
                    sb.AppendLine($"   (not (fact (id {diag.target}_asked)))");
                    sb.AppendLine("   =>");

                    sb.AppendLine($"   (assert (ioproxy (question-id {diag.id}) (messages \"d\") (answers-text \"d\") (answers-react \"d\")))");
                    sb.AppendLine($"   (assert (fact (id {diag.target}_asked) (cf 1.0)))");
                    sb.AppendLine(")");
                }
            }

            // Логические правила
            if (allRules != null)
            {
                foreach (var rule in allRules)
                {
                    sb.AppendLine($"(defrule {rule.id}");
                    List<string> vars = new List<string>();

                    foreach (var condId in rule.conditions)
                    {
                        sb.AppendLine($"   (fact (id {condId}) (cf ?cf_{condId}&:(> ?cf_{condId} 0.2)))");
                        vars.Add($"?cf_{condId}");
                    }
                    sb.AppendLine("   =>");

                    string minFunc = $"min {string.Join(" ", vars)}";
                    double ruleCf = rule.cf > 0 ? rule.cf : 1.0;

                    sb.AppendLine($"   (bind ?m ({minFunc}))");
                    sb.AppendLine($"   (bind ?new_evidence (* ?m {ruleCf.ToString(CultureInfo.InvariantCulture)}))");
                    
                    sb.AppendLine($"   (bind ?ex (find-fact ((?f fact)) (eq ?f:id {rule.conclusion})))");

                    sb.AppendLine("   (if (> (length$ ?ex) 0) then");

                    sb.AppendLine("      (bind ?old_cf (fact-slot-value (nth$ 1 ?ex) cf))");
                    sb.AppendLine("      (bind ?combined_cf (+ ?old_cf (* ?new_evidence (- 1 ?old_cf))))");
                    sb.AppendLine("      (if (> ?combined_cf ?old_cf) then (modify (nth$ 1 ?ex) (cf ?combined_cf)))");

                    sb.AppendLine("   else");
                    sb.AppendLine($"      (assert (fact (id {rule.conclusion}) (text \"dummy\") (cf ?new_evidence)))");
                    sb.AppendLine("   )");
                    sb.AppendLine(")");
                }
            }
            return sb.ToString();
        }

        private void ShowConclusions()
        {
            var evalStr = "(find-all-facts ((?f fact)) (neq ?f:text nil))";
            var result = clips.Eval(evalStr);

            if (result is MultifieldValue mf && mf.Count > 0)
            {
                outputBox.AppendText("\r\n=== РЕЗУЛЬТАТЫ ===\r\n");
                var list = new List<dynamic>();
                for (int i = 0; i < mf.Count; i++)
                {
                    FactAddressValue fv = (FactAddressValue)mf[i];
                    string id = ((LexemeValue)fv["id"]).Value;
                    double cf = ((FloatValue)fv["cf"]).Value;

                    var originalFact = allFacts.FirstOrDefault(f => f.id == id);
                    string text = originalFact != null ? originalFact.text : "Неизвестный факт";

                    list.Add(new { Id = id, Text = text, Cf = cf });
                }

                foreach (var item in list.OrderByDescending(x => x.Cf))
                {
                    string type = "[Вывод]";
                    if (string.Compare(item.Id, "F058") >= 0) type = "[РЕКОМЕНДАЦИЯ]";
                    else if (string.Compare(item.Id, "F053") >= 0) type = "[ЗАКЛЮЧЕНИЕ]";

                    outputBox.AppendText($"{type} {item.Id} ({item.Cf * 100:F1}%): {item.Text}\r\n");
                }
            }
            else outputBox.AppendText("Нет выводов.\r\n");
        }

        private void resetBtn_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow r in factsGrid.Rows)
            {
                r.Cells["Selected"].Value = false;
                r.Cells["CF"].Value = "100";
            }
            outputBox.Clear();
            buttonsPanel.Controls.Clear();
            clips.Clear();
        }

        private void openFile_Click(object sender, EventArgs e) => clipsOpenFileDialog.ShowDialog();
    }
}