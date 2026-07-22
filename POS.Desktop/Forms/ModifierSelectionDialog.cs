using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Desktop.CustomControls;
using POS.Desktop.Themes;

namespace POS.Desktop.Forms;

/// <summary>
/// MOD-001: Modifier selection dialog shown when adding a product that supports modifiers.
/// Displays modifier groups with their options and allows user to select quantities.
/// Returns the selected modifiers, total extra cost, and a summary string.
/// Uses RtlDialog for consistent styling with the rest of the project.
/// </summary>
public static class ModifierSelectionDialog
{
    private record SizeComboItem(string Display, Guid SizeId);

    /// <summary>
    /// Shows the modifier selection dialog.
    /// Returns null if cancelled, or a ModifierSelectionResult with selections and total.
    /// </summary>
    public static ModifierSelectionResult? ShowDialog(IWin32Window owner, ProductDto product, List<ModifierGroupDto> groups)
    {
        ModifierSelectionResult? result = null;

        var selectedModifiers = new Dictionary<Guid, int>();
        var selectedSizes = new Dictionary<Guid, Guid?>();

        using var dialog = new RtlDialog($"إضافة تعديلات - {product.ArabicName ?? product.Name}", 520, 480);

        var headerLabel = new Label
        {
            Text = "اختر التعديلات المطلوبة:",
            Font = DesignTokens.Typography.CardTitle,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Dock = DockStyle.Top,
            Height = 32,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Compact)
        };

        var scrollPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 0, 0, DesignTokens.Spacing.Standard)
        };

        // === Build modifier group sections ===
        foreach (var group in groups)
        {
            var groupSection = BuildGroupSection(group, selectedModifiers, selectedSizes, () => UpdateTotalExtra(dialog, groups, selectedModifiers, selectedSizes));
            scrollPanel.Controls.Add(groupSection);
        }

        // If no groups, show a message
        if (groups.Count == 0)
        {
            scrollPanel.Controls.Add(new Label
            {
                Text = "لا توجد تعديلات متاحة لهذا المنتج",
                Font = DesignTokens.Typography.Body,
                ForeColor = DesignTokens.Colors.TextSecondary,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter,
                Width = 460
            });
        }

        // === Total extra label (placed in the footer area via ContentArea bottom) ===
        var totalExtraLabel = new Label
        {
            Text = "الإجمالي الإضافي: 0.000 JOD",
            Font = DesignTokens.Typography.CardTitle,
            ForeColor = DesignTokens.Colors.Primary,
            Dock = DockStyle.Bottom,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(DesignTokens.Spacing.Standard, 0, 0, 0)
        };

        dialog.ContentArea.Controls.Add(scrollPanel);
        dialog.ContentArea.Controls.Add(headerLabel);
        dialog.ContentArea.Controls.Add(totalExtraLabel);

        // === Dialog actions ===
        dialog.AddAction("✓ تأكيد", (s, e) =>
        {
            if (ValidateSelection(groups, selectedModifiers))
            {
                result = BuildResult(groups, selectedModifiers, selectedSizes);
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            }
        }, isPrimary: true);

        dialog.AddAction("✗ إلغاء", (s, e) =>
        {
            dialog.DialogResult = DialogResult.Cancel;
            dialog.Close();
        }, isPrimary: false);

        dialog.ShowDialog(owner);
        return result;
    }

    private static Panel BuildGroupSection(
        ModifierGroupDto group,
        Dictionary<Guid, int> selectedModifiers,
        Dictionary<Guid, Guid?> selectedSizes,
        Action onChanged)
    {
        var panel = new Panel
        {
            Width = 470,
            Height = 0,
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Compact)
        };

        var groupLabel = new Label
        {
            Text = $"{group.ArabicName ?? group.Name}" +
                   (group.IsRequired ? " (إلزامي)" : "") +
                   (group.MaxSelections > 0 ? $" (حد أقصى: {group.MaxSelections})" : ""),
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleRight
        };
        panel.Controls.Add(groupLabel);

        var innerTop = 28;
        foreach (var modifier in group.Modifiers)
        {
            var modPanel = new Panel
            {
                Width = 440,
                Height = 28,
                Location = new Point(10, innerTop),
                Margin = new Padding(0)
            };

            var priceText = modifier.Price > 0
                ? $"+ {modifier.Price:N3} JOD"
                : "مجاني";

            var modCheckBox = new CheckBox
            {
                Text = $"  {(modifier.ArabicName ?? modifier.Name)}  —  {priceText}",
                Font = DesignTokens.Typography.Body,
                ForeColor = DesignTokens.Colors.TextPrimary,
                Dock = DockStyle.Left,
                Width = 300,
                Height = 24,
                Checked = selectedModifiers.ContainsKey(modifier.Id),
                Tag = modifier.Id,
                RightToLeft = RightToLeft.Yes
            };

            modCheckBox.CheckedChanged += (s, e) =>
            {
                var modId = (Guid)((CheckBox)s!).Tag!;
                if (modCheckBox.Checked)
                {
                    selectedModifiers[modId] = 1;
                    if (modifier.Sizes.Count > 0 && !selectedSizes.ContainsKey(modId))
                        selectedSizes[modId] = modifier.Sizes.First().Id;
                }
                else
                {
                    selectedModifiers.Remove(modId);
                    selectedSizes.Remove(modId);
                }
                onChanged();
            };

            modPanel.Controls.Add(modCheckBox);

            // If the modifier has sizes, add a size combo
            if (modifier.Sizes.Count > 0)
            {
                var sizeCombo = new RtlComboBox
                {
                    Width = 130,
                    Height = 24,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new Point(310, 2),
                    Visible = modCheckBox.Checked,
                    Tag = modifier.Id
                };

                int selectedCmbIdx = 0;
                foreach (var size in modifier.Sizes)
                {
                    var displaySize = $"{size.ArabicName ?? size.Name} " +
                        (size.PriceAdjustment != 0
                            ? $"({(size.PriceAdjustment > 0 ? "+" : "")}{size.PriceAdjustment:N3} JOD)"
                            : "");
                    sizeCombo.Items.Add(new SizeComboItem(displaySize, size.Id));
                    if (size.PriceAdjustment == 0 && size.Price == 0)
                        selectedCmbIdx = sizeCombo.Items.Count - 1;
                }
                sizeCombo.SelectedIndex = selectedCmbIdx;
                sizeCombo.SelectedIndexChanged += (s, e) =>
                {
                    var modId = (Guid)((ComboBox)s!).Tag!;
                    if (sizeCombo.SelectedItem is SizeComboItem sci)
                    {
                        selectedSizes[modId] = sci.SizeId;
                        onChanged();
                    }
                };

                // Show/hide size combo when checkbox changes
                modCheckBox.CheckedChanged += (s, e) =>
                {
                    sizeCombo.Visible = modCheckBox.Checked;
                };

                modPanel.Controls.Add(sizeCombo);
                modPanel.Width = 460;
            }

            panel.Controls.Add(modPanel);
            innerTop += 30;
        }

        panel.Height = innerTop + 4;
        return panel;
    }

    private static void UpdateTotalExtra(
        RtlDialog dialog,
        List<ModifierGroupDto> groups,
        Dictionary<Guid, int> selectedModifiers,
        Dictionary<Guid, Guid?> selectedSizes)
    {
        var total = 0m;
        foreach (var (modId, qty) in selectedModifiers)
        {
            var modifier = groups.SelectMany(g => g.Modifiers).FirstOrDefault(m => m.Id == modId);
            if (modifier == null) continue;

            var basePrice = modifier.Price;

            if (selectedSizes.TryGetValue(modId, out var sizeId) && sizeId.HasValue)
            {
                var size = modifier.Sizes.FirstOrDefault(s => s.Id == sizeId.Value);
                if (size != null)
                    basePrice += size.PriceAdjustment;
            }

            total += basePrice * qty;
        }

        // Find the total label at the bottom of ContentArea
        var totalLabel = dialog.ContentArea.Controls
            .OfType<Label>()
            .FirstOrDefault(l => l.Text.StartsWith("الإجمالي الإضافي"));
        if (totalLabel != null)
            totalLabel.Text = $"الإجمالي الإضافي: {total:N3} JOD";
    }

    private static bool ValidateSelection(
        List<ModifierGroupDto> groups,
        Dictionary<Guid, int> selectedModifiers)
    {
        foreach (var group in groups)
        {
            if (!group.IsRequired) continue;

            var hasSelection = group.Modifiers.Any(m => selectedModifiers.ContainsKey(m.Id));
            if (!hasSelection)
            {
                RtlMessageBox.Show(
                    $"يجب اختيار تعديل واحد على الأقل من مجموعة \"{group.ArabicName ?? group.Name}\"",
                    "حقل إلزامي",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            if (group.MinSelections > 0)
            {
                var count = group.Modifiers.Count(m => selectedModifiers.ContainsKey(m.Id));
                if (count < group.MinSelections)
                {
                    RtlMessageBox.Show(
                        $"يجب اختيار {group.MinSelections} تعديلات على الأقل من \"{group.ArabicName ?? group.Name}\"",
                        "حقل إلزامي",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }
            }

            if (group.MaxSelections > 0)
            {
                var count = group.Modifiers.Count(m => selectedModifiers.ContainsKey(m.Id));
                if (count > group.MaxSelections)
                {
                    RtlMessageBox.Show(
                        $"يمكن اختيار {group.MaxSelections} تعديلات كحد أقصى من \"{group.ArabicName ?? group.Name}\"",
                        "حد أقصى",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }
            }
        }

        return true;
    }

    private static ModifierSelectionResult BuildResult(
        List<ModifierGroupDto> groups,
        Dictionary<Guid, int> selectedModifiers,
        Dictionary<Guid, Guid?> selectedSizes)
    {
        decimal totalExtra = 0;
        var summaryParts = new List<string>();
        var selections = new List<ModifierSelectionDto>();

        foreach (var (modId, qty) in selectedModifiers)
        {
            var modifier = groups.SelectMany(g => g.Modifiers).FirstOrDefault(m => m.Id == modId);
            if (modifier == null) continue;

            var basePrice = modifier.Price;
            Guid? sizeId = null;

            if (selectedSizes.TryGetValue(modId, out var sid) && sid.HasValue)
            {
                sizeId = sid;
                var size = modifier.Sizes.FirstOrDefault(s => s.Id == sid.Value);
                if (size != null)
                    basePrice += size.PriceAdjustment;
            }

            totalExtra += basePrice * qty;
            selections.Add(new ModifierSelectionDto(modId, sizeId, qty));

            var modifierName = modifier.ArabicName ?? modifier.Name;
            if (sizeId.HasValue)
            {
                var foundSize = modifier.Sizes.FirstOrDefault(s => s.Id == sizeId.Value);
                if (foundSize != null)
                    modifierName += $" ({foundSize.ArabicName ?? foundSize.Name})";
            }
            summaryParts.Add(modifierName);
        }

        return new ModifierSelectionResult(selections, totalExtra, string.Join("، ", summaryParts));
    }
}
