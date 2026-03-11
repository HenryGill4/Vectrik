// dynamic-form-renderer.js - Renders CustomFieldsConfig JSON forms
// Used by the generic stage fallback when JavaScript rendering is needed.

window.DynamicFormRenderer = {
    render: function (containerId, fieldsJson) {
        const container = document.getElementById(containerId);
        if (!container) return;
        container.innerHTML = '';

        let fields;
        try { fields = JSON.parse(fieldsJson); } catch { container.innerHTML = '<p>Invalid form config</p>'; return; }
        if (!Array.isArray(fields) || fields.length === 0) { container.innerHTML = '<p>No fields configured</p>'; return; }

        const grid = document.createElement('div');
        grid.style.cssText = 'display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:12px;';

        fields.forEach(function (f) {
            const group = document.createElement('div');
            group.className = 'form-group';

            const label = document.createElement('label');
            label.className = 'form-label';
            label.textContent = f.label + (f.required ? ' *' : '');
            group.appendChild(label);

            let input;
            switch ((f.type || 'text').toLowerCase()) {
                case 'dropdown':
                    input = document.createElement('select');
                    input.className = 'form-control';
                    const empty = document.createElement('option');
                    empty.value = '';
                    empty.textContent = 'Select...';
                    input.appendChild(empty);
                    (f.options || []).forEach(function (opt) {
                        const o = document.createElement('option');
                        o.textContent = opt;
                        input.appendChild(o);
                    });
                    break;
                case 'checkbox':
                    input = document.createElement('input');
                    input.type = 'checkbox';
                    input.style.cssText = 'width:20px;height:20px;margin-top:8px;';
                    break;
                case 'number':
                    input = document.createElement('input');
                    input.className = 'form-control';
                    input.type = 'number';
                    input.inputMode = 'numeric';
                    if (f.min !== undefined) input.min = f.min;
                    if (f.max !== undefined) input.max = f.max;
                    break;
                default:
                    input = document.createElement('input');
                    input.className = 'form-control';
                    input.type = 'text';
                    break;
            }

            input.name = f.name;
            input.dataset.fieldName = f.name;
            if (f.required) input.required = true;
            group.appendChild(input);
            grid.appendChild(group);
        });

        container.appendChild(grid);
    },

    collectValues: function (containerId) {
        const container = document.getElementById(containerId);
        if (!container) return '{}';
        const values = {};
        container.querySelectorAll('[data-field-name]').forEach(function (el) {
            const name = el.dataset.fieldName;
            if (el.type === 'checkbox') values[name] = el.checked;
            else values[name] = el.value;
        });
        return JSON.stringify(values);
    }
};
