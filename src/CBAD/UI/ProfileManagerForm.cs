using CBAD.Models;

namespace CBAD.UI;

/// <summary>
/// Dialog that lets the user create, edit, delete, and activate battery profiles.
/// Each profile stores the Cadex 7400 C-code test parameters for a battery pack.
/// </summary>
internal sealed class ProfileManagerForm : Form
{
    // ── Model ─────────────────────────────────────────────────────────────
    private readonly ProfileManager _manager;

    // ── Profile-selection strip ───────────────────────────────────────────
    private readonly ComboBox _cbProfiles    = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly Button   _btnNew        = new() { Text = "＋ New",       AutoSize = true, Height = 28 };
    private readonly Button   _btnSetActive  = new() { Text = "✔ Set Active", AutoSize = true, Height = 28 };

    // ── Detail fields ─────────────────────────────────────────────────────
    private readonly TextBox       _txtName             = new() { Width = 200 };
    private readonly TextBox       _txtBatteryType      = new() { Width = 200 };
    private readonly NumericUpDown _numTargetCapPercentage = CreateNum(0,    100,    1,    0);
    private readonly NumericUpDown _numVolts            = CreateNum(0,    999,    0.1,  2);
    private readonly NumericUpDown _numCapacityMAh      = CreateNum(0,    999999, 1,    0);  // large packs can be 100 000+ mAh
    private readonly NumericUpDown _numChargeC          = CreateNum(0,    1,      0.05, 2);
    private readonly NumericUpDown _numDischargeC       = CreateNum(0,    1,      0.05, 2);
    private readonly NumericUpDown _numMinTemp          = CreateNum(-50,  150,    1,    1);
    private readonly NumericUpDown _numMaxTemp          = CreateNum(-50,  150,    1,    1);
    private readonly NumericUpDown _numMaxStandbyV      = CreateNum(0,    10,     0.01, 3);
    private readonly NumericUpDown _numMaxChargeV       = CreateNum(0,    10,     0.01, 3);
    private readonly NumericUpDown _numEndOfChargeC     = CreateNum(0,    1,      0.01, 3);
    private readonly NumericUpDown _numEndOfDischargeV  = CreateNum(0,    10,     0.01, 3);

    // ── Action buttons ────────────────────────────────────────────────────
    private readonly Button _btnSave   = new() { Text = "💾 Save",   AutoSize = true, Height = 30 };
    private readonly Button _btnDelete = new() { Text = "🗑 Delete", AutoSize = true, Height = 30 };
    private readonly Button _btnClose  = new() { Text = "Close",     Width = 80,     Height = 30 };

    // ── Active-profile display label ──────────────────────────────────────
    private readonly Label _lblActive = new() { AutoSize = true, ForeColor = Color.FromArgb(34, 139, 34) };

    // ── Constructor ───────────────────────────────────────────────────────
    public ProfileManagerForm(ProfileManager manager)
    {
        _manager = manager;

        Text            = "Battery Profile Manager";
        Width           = 600;
        Height          = 600;
        MinimumSize     = new Size(560, 550);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;

        BuildUi();
        RefreshProfileList(selectedName: _manager.ActiveProfileName);
    }

    // ── UI construction ───────────────────────────────────────────────────
    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 4,
            Padding     = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // profile selector strip
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // active label
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // scrollable detail panel
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // button strip
        Controls.Add(root);

        // Row 0: profile selector ─────────────────────────────────────────
        var selectorFlow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            AutoSize      = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            Padding       = new Padding(0, 0, 0, 6),
        };

        selectorFlow.Controls.Add(new Label
        {
            Text     = "Profile:",
            AutoSize = true,
            Margin   = new Padding(0, 6, 6, 0),
        });

        _cbProfiles.Margin = new Padding(0, 3, 8, 0);
        _cbProfiles.SelectedIndexChanged += OnProfileSelected;
        selectorFlow.Controls.Add(_cbProfiles);

        StyleSecondaryButton(_btnNew);
        _btnNew.Margin = new Padding(0, 3, 6, 0);
        _btnNew.Click += OnNewClicked;
        selectorFlow.Controls.Add(_btnNew);

        StylePrimaryButton(_btnSetActive);
        _btnSetActive.Margin = new Padding(0, 3, 0, 0);
        _btnSetActive.Click += OnSetActiveClicked;
        selectorFlow.Controls.Add(_btnSetActive);

        root.Controls.Add(selectorFlow, 0, 0);

        // Row 1: active profile notice ────────────────────────────────────
        _lblActive.Margin = new Padding(2, 0, 0, 8);
        root.Controls.Add(_lblActive, 0, 1);

        // Row 2: detail fields (scrollable) ───────────────────────────────
        var scroll = new Panel
        {
            Dock      = DockStyle.Fill,
            AutoScroll = true,
            BorderStyle = BorderStyle.FixedSingle,
        };

        var group = new GroupBox
        {
            Text    = "Profile Details",
            Dock    = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 10),
        };

        var grid = new TableLayoutPanel
        {
            Dock        = DockStyle.Top,
            AutoSize    = true,
            ColumnCount = 2,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        AddField(grid, "Profile Name",               _txtName,            row: 0);
        AddField(grid, "Battery Type",               _txtBatteryType,     row: 1);
        AddField(grid, "Target Capacity (%)",        _numTargetCapPercentage, row: 2);
        AddField(grid, "Volts (V)",                  _numVolts,           row: 3);
        AddField(grid, "Capacity (mAh)",             _numCapacityMAh,     row: 4);
        AddField(grid, "Charge Current (C)",         _numChargeC,         row: 5);
        AddField(grid, "Discharge Current (C)",      _numDischargeC,      row: 6);
        AddField(grid, "Min Temp (°C)",              _numMinTemp,         row: 7);
        AddField(grid, "Max Temp (°C)",              _numMaxTemp,         row: 8);
        AddField(grid, "Max Standby Voltage/Cell",   _numMaxStandbyV,     row: 9);
        AddField(grid, "Max Charge Voltage/Cell",    _numMaxChargeV,      row: 10);
        AddField(grid, "End of Charge (C)",          _numEndOfChargeC,    row: 11);
        AddField(grid, "End of Discharge Voltage/Cell", _numEndOfDischargeV, row: 12);

        group.Controls.Add(grid);
        scroll.Controls.Add(group);
        root.Controls.Add(scroll, 0, 2);

        // Row 3: action buttons ────────────────────────────────────────────
        var btnFlow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize      = true,
            Padding       = new Padding(0, 8, 0, 0),
        };

        _btnClose.Margin = new Padding(4, 0, 0, 0);
        _btnClose.Click += (_, __) => Close();

        StyleDangerButton(_btnDelete);
        _btnDelete.Margin = new Padding(4, 0, 4, 0);
        _btnDelete.Click += OnDeleteClicked;

        StylePrimaryButton(_btnSave);
        _btnSave.Margin = new Padding(4, 0, 4, 0);
        _btnSave.Click += OnSaveClicked;

        btnFlow.Controls.Add(_btnClose);
        btnFlow.Controls.Add(_btnDelete);
        btnFlow.Controls.Add(_btnSave);

        root.Controls.Add(btnFlow, 0, 3);
    }

    // ── Profile-list management ───────────────────────────────────────────
    private void RefreshProfileList(string? selectedName = null)
    {
        _cbProfiles.SelectedIndexChanged -= OnProfileSelected;
        _cbProfiles.Items.Clear();

        foreach (var p in _manager.Profiles)
            _cbProfiles.Items.Add(p.ProfileName);

        if (selectedName is not null && _cbProfiles.Items.Contains(selectedName))
            _cbProfiles.SelectedItem = selectedName;
        else if (_cbProfiles.Items.Count > 0)
            _cbProfiles.SelectedIndex = 0;

        _cbProfiles.SelectedIndexChanged += OnProfileSelected;

        // Populate fields for whatever ended up selected
        if (_cbProfiles.SelectedItem is string name)
            LoadProfileIntoFields(_manager.Get(name));
        else
            ClearFields();

        UpdateActiveLabel();
        UpdateButtonStates();
    }

    private void LoadProfileIntoFields(BatteryProfile? p)
    {
        if (p is null) { ClearFields(); return; }

        _txtName.Text                = p.ProfileName;
        _txtBatteryType.Text         = p.BatteryType;
        SetNum(_numTargetCapPercentage, (decimal)p.TargetCapPercentage);
        SetNum(_numVolts,            (decimal)p.Volts);
        SetNum(_numCapacityMAh,      (decimal)p.CapacityMAh);
        SetNum(_numChargeC,          (decimal)p.ChargeCurrentC);
        SetNum(_numDischargeC,       (decimal)p.DischargeCurrentC);
        SetNum(_numMinTemp,          (decimal)p.MinTempCelsius);
        SetNum(_numMaxTemp,          (decimal)p.MaxTempCelsius);
        SetNum(_numMaxStandbyV,      (decimal)p.MaxStandbyVoltagePerCell);
        SetNum(_numMaxChargeV,       (decimal)p.MaxChargeVoltagePerCell);
        SetNum(_numEndOfChargeC,     (decimal)p.EndOfChargeC);
        SetNum(_numEndOfDischargeV,  (decimal)p.EndOfDischargeVoltagePerCell);
    }

    private void ClearFields()
    {
        _txtName.Text        = string.Empty;
        _txtBatteryType.Text = string.Empty;
        foreach (var n in new[] { _numTargetCapPercentage, _numVolts, _numCapacityMAh,
                                  _numChargeC, _numDischargeC,
                                  _numMinTemp, _numMaxTemp,
                                  _numMaxStandbyV, _numMaxChargeV,
                                  _numEndOfChargeC, _numEndOfDischargeV })
            n.Value = n.Minimum;
    }

    private BatteryProfile BuildProfileFromFields()
    {
        var name = _txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Profile Name is required.");

        return new BatteryProfile
        {
            ProfileName                 = name,
            BatteryType                 = _txtBatteryType.Text.Trim(),
            TargetCapPercentage         = (double)_numTargetCapPercentage.Value,
            Volts                       = (double)_numVolts.Value,
            CapacityMAh                 = (double)_numCapacityMAh.Value,
            ChargeCurrentC              = (double)_numChargeC.Value,
            DischargeCurrentC           = (double)_numDischargeC.Value,
            MinTempCelsius              = (double)_numMinTemp.Value,
            MaxTempCelsius              = (double)_numMaxTemp.Value,
            MaxStandbyVoltagePerCell    = (double)_numMaxStandbyV.Value,
            MaxChargeVoltagePerCell     = (double)_numMaxChargeV.Value,
            EndOfChargeC                = (double)_numEndOfChargeC.Value,
            EndOfDischargeVoltagePerCell = (double)_numEndOfDischargeV.Value,
        };
    }

    private void UpdateActiveLabel()
    {
        var active = _manager.ActiveProfileName;
        _lblActive.Text = active is null
            ? "No active profile set."
            : $"Active profile: {active}";
    }

    private void UpdateButtonStates()
    {
        bool hasProfiles = _cbProfiles.Items.Count > 0;
        _btnDelete.Enabled   = hasProfiles;
        _btnSave.Enabled     = true;
        _btnSetActive.Enabled = hasProfiles;
    }

    // ── Event handlers ────────────────────────────────────────────────────
    private void OnProfileSelected(object? sender, EventArgs e)
    {
        if (_cbProfiles.SelectedItem is string name)
            LoadProfileIntoFields(_manager.Get(name));
    }

    private void OnNewClicked(object? sender, EventArgs e)
    {
        ClearFields();
        _cbProfiles.SelectedIndex = -1;
        _txtName.Focus();
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            var profile = BuildProfileFromFields();
            var existing = _manager.Get(profile.ProfileName);

            if (existing is null)
                _manager.Add(profile);
            else
                _manager.Update(profile);

            RefreshProfileList(selectedName: profile.ProfileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_cbProfiles.SelectedItem is not string name)
            return;

        var confirm = MessageBox.Show(
            this,
            $"Delete profile '{name}'?",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
            return;

        try
        {
            _manager.Delete(name);
            RefreshProfileList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Delete Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnSetActiveClicked(object? sender, EventArgs e)
    {
        if (_cbProfiles.SelectedItem is not string name)
            return;

        try
        {
            _manager.SetActive(name);
            UpdateActiveLabel();
            MessageBox.Show(this, $"'{name}' is now the active profile.", "Active Profile Set",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static NumericUpDown CreateNum(double min, double max, double increment, int decimalPlaces)
    {
        return new NumericUpDown
        {
            Minimum       = (decimal)min,
            Maximum       = (decimal)max,
            Increment     = (decimal)increment,
            DecimalPlaces = decimalPlaces,
            Width         = 120,
        };
    }

    private static void SetNum(NumericUpDown num, decimal value)
    {
        num.Value = Math.Clamp(value, num.Minimum, num.Maximum);
    }

    private static void AddField(TableLayoutPanel grid, string labelText, Control control, int row)
    {
        var lbl = new Label
        {
            Text     = labelText,
            AutoSize = true,
            Margin   = new Padding(3, 8, 6, 3),
            Anchor   = AnchorStyles.Left | AnchorStyles.Top,
        };
        grid.Controls.Add(lbl, 0, row);

        control.Margin = new Padding(3, 5, 3, 5);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        grid.Controls.Add(control, 1, row);
    }

    private static void StylePrimaryButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = Color.FromArgb(34, 100, 180);
        btn.ForeColor = Color.White;
        btn.FlatAppearance.BorderColor = Color.FromArgb(20, 70, 140);
    }

    private static void StyleSecondaryButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = Color.FromArgb(55, 72, 100);
        btn.ForeColor = Color.White;
        btn.FlatAppearance.BorderColor = Color.FromArgb(90, 110, 155);
    }

    private static void StyleDangerButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = Color.FromArgb(180, 40, 40);
        btn.ForeColor = Color.White;
        btn.FlatAppearance.BorderColor = Color.FromArgb(120, 20, 20);
    }
}
