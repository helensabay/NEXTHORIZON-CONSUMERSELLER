# Quick Actions Integration Checklist

## ✅ Completed Setup

- [x] JavaScript functions added to `wwwroot/js/app.js`
  - toggleQuickActions()
  - insertText()
  - quickActionSelect()

- [x] CSS styling added to `Views/Shared/_Layout.cshtml`
  - All quick actions classes defined
  - Responsive design implemented
  - Dark theme with blue hover effect

- [x] C# Model created: `Models/QuickActionsViewModel.cs`
  - QuickActionsViewModel class
  - QuickActionButton class

- [x] Partial view created: `Views/Shared/_QuickActionsPartial.cshtml`
  - Reusable component
  - HTML example in comments

- [x] Example view: `Views/Home/QuickActionsExample.cshtml`
  - 3 working examples
  - Integration instructions
  - API reference

---

## 🔄 Integration Steps (For Your Forms)

### Step 1: Identify Target Forms
Choose which forms need Quick Actions:
- [ ] Messaging/Chat input
- [ ] Product review textarea
- [ ] Seller shop description
- [ ] Support ticket form
- [ ] Other: _____________

### Step 2: Choose Implementation Method

**Option A: Using Partial View (Recommended)**
```csharp
@{
    var quickActions = new QuickActionsViewModel
    {
        InputId = "yourInputId",
        Buttons = new[]
        {
            new QuickActionButton { Label = "Button1", Value = "Text to insert" },
            new QuickActionButton { Label = "Button2", Value = "Another text" }
        }
    };
}

@await Html.PartialAsync("_QuickActionsPartial", quickActions)
<textarea id="yourInputId" class="form-input"></textarea>
```

**Option B: Direct HTML**
```html
<div class="quick-actions-group">
    <div class="quick-actions-label">
        <span>QUICK ACTIONS</span>
        <button class="quick-actions-toggle" onclick="toggleQuickActions(this)" type="button">Hide</button>
    </div>
    <div class="quick-actions-container">
        <button class="quick-action-btn" onclick="insertText('Your text', 'inputId')" type="button">Label</button>
    </div>
</div>
<input type="text" id="inputId" class="form-input">
```

### Step 3: Define Button Labels and Values
For each form, create appropriate quick action buttons:

**Example: Messaging Form**
| Button Label | Value/Text | Type |
|---|---|---|
| Greeting | "Hi! How can I help?" | insertText |
| Thanks | "Thank you for your inquiry!" | insertText |
| Interested | "I'm interested in this." | insertText |
| Price | "What's the price?" | insertText |

### Step 4: Add to Form Views
- [ ] Locate the view file (.cshtml)
- [ ] Find the textarea/input element
- [ ] Add quick actions component above it
- [ ] Update InputId to match the input ID
- [ ] Customize button labels and values for your use case

### Step 5: Test
- [ ] Buttons appear above input field
- [ ] Clicking buttons inserts text at cursor
- [ ] Hide/Show toggle works
- [ ] Mobile layout is responsive
- [ ] No JavaScript errors in console

---

## 📋 Common Implementation Patterns

### Messaging Input
```html
<div class="quick-actions-group">
    <div class="quick-actions-label"><span>QUICK ACTIONS</span><button class="quick-actions-toggle" onclick="toggleQuickActions(this)">Hide</button></div>
    <div class="quick-actions-container">
        <button class="quick-action-btn" onclick="insertText('Hi there!', 'msgInput')">Hi</button>
        <button class="quick-action-btn" onclick="insertText('Thanks!', 'msgInput')">Thanks</button>
        <button class="quick-action-btn" onclick="insertText('Can you help?', 'msgInput')">Help</button>
    </div>
</div>
<input type="text" id="msgInput" class="form-input" placeholder="Type message...">
```

### Product Description
```html
<div class="quick-actions-group">
    <div class="quick-actions-label"><span>QUICK ACTIONS</span><button class="quick-actions-toggle" onclick="toggleQuickActions(this)">Hide</button></div>
    <div class="quick-actions-container">
        <button class="quick-action-btn" onclick="insertText('Material: ', 'desc')">Material</button>
        <button class="quick-action-btn" onclick="insertText('Size: ', 'desc')">Size</button>
        <button class="quick-action-btn" onclick="insertText('Care: ', 'desc')">Care</button>
    </div>
</div>
<textarea id="desc" class="form-input" placeholder="Product description..."></textarea>
```

### Status/Select Field
```html
<div class="quick-actions-group">
    <div class="quick-actions-label"><span>SET STATUS</span><button class="quick-actions-toggle" onclick="toggleQuickActions(this)">Hide</button></div>
    <div class="quick-actions-container">
        <button class="quick-action-btn" onclick="quickActionSelect('shipped', 'status')">Shipped</button>
        <button class="quick-action-btn" onclick="quickActionSelect('delivered', 'status')">Delivered</button>
        <button class="quick-action-btn" onclick="quickActionSelect('pending', 'status')">Pending</button>
    </div>
</div>
<select id="status" class="form-input">
    <option value="">Select status...</option>
    <option value="pending">Pending</option>
    <option value="shipped">Shipped</option>
    <option value="delivered">Delivered</option>
</select>
```

---

## 🐛 Troubleshooting

**Problem:** Buttons don't insert text
- **Solution:** Check that `inputId` in onclick matches the input element's `id` attribute

**Problem:** Apostrophes breaking the onclick
- **Solution:** Use `\'` to escape: `onclick="insertText('can\'t', 'id')"`

**Problem:** Functions not found error
- **Solution:** Verify `app.js` is loaded in your layout and functions are present

**Problem:** Buttons not visible
- **Solution:** Check that CSS classes `.quick-actions-group`, `.quick-action-btn` are loaded from _Layout.cshtml

**Problem:** Hide button not working
- **Solution:** Ensure `toggleQuickActions(this)` is passed to the button onclick handler

---

## 📚 Reference Files

- **JavaScript:** `wwwroot/js/app.js` (lines near end of file)
- **CSS:** `Views/Shared/_Layout.cshtml` (search for ".quick-actions-group")
- **Partial View:** `Views/Shared/_QuickActionsPartial.cshtml`
- **Models:** `Models/QuickActionsViewModel.cs`
- **Examples:** `Views/Home/QuickActionsExample.cshtml`

---

## 🎨 Customization

### Change Colors
Edit in `_Layout.cshtml`:
```css
.quick-action-btn {
    background: #1a1a1a;  /* Default background */
    color: #fff;
}

.quick-action-btn:hover {
    background: #0f4a87;  /* Hover background */
}
```

### Change Button Size
```css
.quick-action-btn {
    padding: 6px 14px;     /* Default: 6px 14px */
    font-size: 13px;       /* Default: 13px */
}
```

### Adjust Responsive Breakpoints
```css
@@media (max-width: 600px) {
    /* Mobile styles */
}
```

---

## ✨ Next Steps

1. **View Examples:** Visit `/Home/QuickActionsExample` to see working examples
2. **Choose Forms:** Identify which forms need Quick Actions
3. **Define Buttons:** Create button labels and values for each form
4. **Implement:** Add quick actions to your form views
5. **Test:** Verify functionality on desktop and mobile
6. **Deploy:** Push changes to production

---

*Quick Actions Implementation v1.0*
*All setup files created and ready for integration*
