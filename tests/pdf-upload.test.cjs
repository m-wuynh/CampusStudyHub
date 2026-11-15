const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const script = fs.readFileSync(path.join(__dirname, '../StudyHub.Web/wwwroot/js/pdf/pdf-upload.js'), 'utf8');
const limit = 25 * 1024 * 1024;

function setup() {
    const handlers = {}, attributes = {};
    const error = { id: 'upload-file-error', hidden: true, textContent: '' };
    const input = {
        files: [],
        classList: { toggle(name, enabled) { input.invalid = enabled; } },
        setAttribute(name, value) { attributes[name] = value; },
        removeAttribute(name) { delete attributes[name]; },
        addEventListener(name, handler) { handlers[name] = handler; },
        focus() { input.focused = true; }
    };
    const form = {
        dataset: { maxFileBytes: String(limit) },
        querySelector(selector) { return selector === 'input[type="file"]' ? input : error; },
        addEventListener(name, handler) { handlers[name] = handler; }
    };
    vm.runInNewContext(script, { document: { querySelector: () => form } });
    const submit = () => {
        let prevented = false;
        handlers.submit({ preventDefault() { prevented = true; } });
        return prevented;
    };
    return { input, error, attributes, handlers, submit };
}

test('oversized selection shows an inline error and blocks navigation on submit', () => {
    const ui = setup();
    ui.input.files = [{ size: limit + 1 }];
    ui.handlers.change();
    assert.equal(ui.error.hidden, false);
    assert.match(ui.error.textContent, /25 MiB/);
    assert.equal(ui.attributes['aria-invalid'], 'true');
    assert.equal(ui.submit(), true);
    assert.equal(ui.input.focused, true);
});

test('submission checks file size even without a change event', () => {
    const ui = setup();
    ui.input.files = [{ size: 40 * 1024 * 1024 }];
    assert.equal(ui.submit(), true);
});

test('replacing an oversized file with a file at the limit clears errors and permits upload', () => {
    const ui = setup();
    ui.input.files = [{ size: limit + 1 }];
    ui.handlers.change();
    ui.input.files = [{ size: limit }];
    ui.handlers.change();
    assert.equal(ui.error.hidden, true);
    assert.equal(ui.input.invalid, false);
    assert.equal(ui.attributes['aria-invalid'], 'false');
    assert.equal(ui.submit(), false);
});

test('clearing the selection clears the size error', () => {
    const ui = setup();
    ui.input.files = [{ size: limit + 1 }];
    ui.handlers.change();
    ui.input.files = [];
    ui.handlers.change();
    assert.equal(ui.error.hidden, true);
    assert.equal(ui.error.textContent, '');
});
