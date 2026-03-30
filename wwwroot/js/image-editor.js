// image-editor.js — Canvas-based image crop/rotate/zoom editor.
// Loaded as an ES module from Blazor via IJSRuntime.

let _dotNetRef = null;
let _canvas = null;
let _ctx = null;
let _img = null;

// Transform state
let _rotation = 0;       // 0, 90, 180, 270
let _flipH = false;
let _flipV = false;
let _zoom = 1.0;
let _panX = 0;
let _panY = 0;

// Crop state
let _cropActive = false;
let _cropRect = null;     // { x, y, w, h } in image-space
let _dragType = null;     // 'create' | 'move' | 'nw' | 'ne' | 'sw' | 'se' | 'n' | 's' | 'e' | 'w'
let _dragStart = null;
let _cropStartRect = null;

// Canvas display
let _canvasW = 0;
let _canvasH = 0;
let _scale = 1;           // image-to-canvas scale
let _offsetX = 0;
let _offsetY = 0;

// Pan state
let _isPanning = false;
let _panStart = null;

const HANDLE_SIZE = 8;
const MIN_CROP = 20;

export function init(dotNetRef, canvasId) {
    _dotNetRef = dotNetRef;
    _canvas = document.getElementById(canvasId);
    if (!_canvas) return;
    _ctx = _canvas.getContext('2d');

    _canvas.addEventListener('mousedown', onMouseDown);
    _canvas.addEventListener('mousemove', onMouseMove);
    _canvas.addEventListener('mouseup', onMouseUp);
    _canvas.addEventListener('mouseleave', onMouseUp);
    _canvas.addEventListener('wheel', onWheel, { passive: false });

    // Touch support
    _canvas.addEventListener('touchstart', onTouchStart, { passive: false });
    _canvas.addEventListener('touchmove', onTouchMove, { passive: false });
    _canvas.addEventListener('touchend', onTouchEnd);
}

export function loadImage(base64Data) {
    return new Promise((resolve) => {
        _img = new Image();
        _img.onload = () => {
            resetTransform();
            render();
            resolve(true);
        };
        _img.onerror = () => resolve(false);
        _img.src = base64Data;
    });
}

export function rotate(degrees) {
    _rotation = ((_rotation + degrees) % 360 + 360) % 360;
    _cropRect = null;
    _cropActive = false;
    fitImage();
    render();
}

export function flipHorizontal() {
    _flipH = !_flipH;
    render();
}

export function flipVertical() {
    _flipV = !_flipV;
    render();
}

export function zoomIn() {
    _zoom = Math.min(_zoom * 1.25, 5.0);
    fitImage();
    render();
}

export function zoomOut() {
    _zoom = Math.max(_zoom / 1.25, 0.2);
    fitImage();
    render();
}

export function zoomFit() {
    _zoom = 1.0;
    _panX = 0;
    _panY = 0;
    fitImage();
    render();
}

export function toggleCrop() {
    _cropActive = !_cropActive;
    if (!_cropActive) {
        _cropRect = null;
    }
    render();
    return _cropActive;
}

export function setCropActive(active) {
    _cropActive = active;
    if (!active) _cropRect = null;
    render();
}

export function resetAll() {
    _rotation = 0;
    _flipH = false;
    _flipV = false;
    _zoom = 1.0;
    _panX = 0;
    _panY = 0;
    _cropRect = null;
    _cropActive = false;
    fitImage();
    render();
}

export async function exportImage(format, quality) {
    if (!_img) return null;

    const fmt = format || 'image/png';
    const q = quality || 0.92;

    // Determine effective image dimensions after rotation
    const rotated = _rotation === 90 || _rotation === 270;
    let srcW = rotated ? _img.height : _img.width;
    let srcH = rotated ? _img.width : _img.height;

    let cropX = 0, cropY = 0, cropW = srcW, cropH = srcH;

    if (_cropRect) {
        cropX = Math.max(0, Math.round(_cropRect.x));
        cropY = Math.max(0, Math.round(_cropRect.y));
        cropW = Math.min(srcW - cropX, Math.round(_cropRect.w));
        cropH = Math.min(srcH - cropY, Math.round(_cropRect.h));
    }

    // Create export canvas
    const expCanvas = document.createElement('canvas');
    expCanvas.width = cropW;
    expCanvas.height = cropH;
    const expCtx = expCanvas.getContext('2d');

    // Apply transforms and draw
    expCtx.save();
    expCtx.translate(-cropX, -cropY);
    applyImageTransform(expCtx, srcW, srcH);
    expCtx.drawImage(_img, 0, 0);
    expCtx.restore();

    return expCanvas.toDataURL(fmt, q);
}

export function dispose() {
    if (_canvas) {
        _canvas.removeEventListener('mousedown', onMouseDown);
        _canvas.removeEventListener('mousemove', onMouseMove);
        _canvas.removeEventListener('mouseup', onMouseUp);
        _canvas.removeEventListener('mouseleave', onMouseUp);
        _canvas.removeEventListener('wheel', onWheel);
        _canvas.removeEventListener('touchstart', onTouchStart);
        _canvas.removeEventListener('touchmove', onTouchMove);
        _canvas.removeEventListener('touchend', onTouchEnd);
    }
    _dotNetRef = null;
    _canvas = null;
    _ctx = null;
    _img = null;
}

// ── Internal ─────────────────────────────────────────

function resetTransform() {
    _rotation = 0;
    _flipH = false;
    _flipV = false;
    _zoom = 1.0;
    _panX = 0;
    _panY = 0;
    _cropRect = null;
    _cropActive = false;
    fitImage();
}

function getDisplayDimensions() {
    const rotated = _rotation === 90 || _rotation === 270;
    return {
        w: rotated ? _img.height : _img.width,
        h: rotated ? _img.width : _img.height
    };
}

function fitImage() {
    if (!_canvas || !_img) return;

    const container = _canvas.parentElement;
    _canvasW = container.clientWidth || 600;
    _canvasH = container.clientHeight || 400;
    _canvas.width = _canvasW;
    _canvas.height = _canvasH;

    const dim = getDisplayDimensions();
    const scaleX = _canvasW / dim.w;
    const scaleY = _canvasH / dim.h;
    _scale = Math.min(scaleX, scaleY, 1) * _zoom;

    _offsetX = (_canvasW - dim.w * _scale) / 2 + _panX;
    _offsetY = (_canvasH - dim.h * _scale) / 2 + _panY;
}

function applyImageTransform(ctx, displayW, displayH) {
    // Center of display area
    const cx = displayW / 2;
    const cy = displayH / 2;

    ctx.translate(cx, cy);
    ctx.rotate((_rotation * Math.PI) / 180);
    ctx.scale(_flipH ? -1 : 1, _flipV ? -1 : 1);

    const rotated = _rotation === 90 || _rotation === 270;
    const drawW = rotated ? displayH : displayW;
    const drawH = rotated ? displayW : displayH;
    ctx.translate(-drawW / 2, -drawH / 2);
}

function render() {
    if (!_ctx || !_img) return;
    fitImage();

    _ctx.clearRect(0, 0, _canvasW, _canvasH);

    // Draw checkerboard background
    drawCheckerboard();

    const dim = getDisplayDimensions();

    _ctx.save();
    _ctx.translate(_offsetX, _offsetY);
    _ctx.scale(_scale, _scale);

    applyImageTransform(_ctx, dim.w, dim.h);
    _ctx.drawImage(_img, 0, 0);
    _ctx.restore();

    // Draw crop overlay
    if (_cropActive && _cropRect) {
        drawCropOverlay();
    }
}

function drawCheckerboard() {
    const size = 10;
    _ctx.fillStyle = '#1a1a2e';
    _ctx.fillRect(0, 0, _canvasW, _canvasH);
    _ctx.fillStyle = '#22223a';
    for (let y = 0; y < _canvasH; y += size * 2) {
        for (let x = 0; x < _canvasW; x += size * 2) {
            _ctx.fillRect(x, y, size, size);
            _ctx.fillRect(x + size, y + size, size, size);
        }
    }
}

function drawCropOverlay() {
    if (!_cropRect) return;

    // Convert crop rect from image-space to canvas-space
    const cx = _offsetX + _cropRect.x * _scale;
    const cy = _offsetY + _cropRect.y * _scale;
    const cw = _cropRect.w * _scale;
    const ch = _cropRect.h * _scale;

    // Dim outside crop area
    _ctx.fillStyle = 'rgba(0, 0, 0, 0.55)';
    // Top
    _ctx.fillRect(0, 0, _canvasW, cy);
    // Bottom
    _ctx.fillRect(0, cy + ch, _canvasW, _canvasH - cy - ch);
    // Left
    _ctx.fillRect(0, cy, cx, ch);
    // Right
    _ctx.fillRect(cx + cw, cy, _canvasW - cx - cw, ch);

    // Crop border
    _ctx.strokeStyle = '#fff';
    _ctx.lineWidth = 2;
    _ctx.strokeRect(cx, cy, cw, ch);

    // Rule-of-thirds grid
    _ctx.strokeStyle = 'rgba(255, 255, 255, 0.3)';
    _ctx.lineWidth = 1;
    for (let i = 1; i <= 2; i++) {
        // Vertical
        _ctx.beginPath();
        _ctx.moveTo(cx + (cw * i) / 3, cy);
        _ctx.lineTo(cx + (cw * i) / 3, cy + ch);
        _ctx.stroke();
        // Horizontal
        _ctx.beginPath();
        _ctx.moveTo(cx, cy + (ch * i) / 3);
        _ctx.lineTo(cx + cw, cy + (ch * i) / 3);
        _ctx.stroke();
    }

    // Corner handles
    const hs = HANDLE_SIZE;
    _ctx.fillStyle = '#fff';
    const handles = [
        [cx - hs / 2, cy - hs / 2],                     // nw
        [cx + cw - hs / 2, cy - hs / 2],                // ne
        [cx - hs / 2, cy + ch - hs / 2],                // sw
        [cx + cw - hs / 2, cy + ch - hs / 2],           // se
    ];
    handles.forEach(([hx, hy]) => {
        _ctx.fillRect(hx, hy, hs, hs);
    });

    // Edge handles
    const edgeHandles = [
        [cx + cw / 2 - hs / 2, cy - hs / 2],           // n
        [cx + cw / 2 - hs / 2, cy + ch - hs / 2],      // s
        [cx - hs / 2, cy + ch / 2 - hs / 2],            // w
        [cx + cw - hs / 2, cy + ch / 2 - hs / 2],      // e
    ];
    edgeHandles.forEach(([hx, hy]) => {
        _ctx.fillRect(hx, hy, hs, hs);
    });

    // Dimensions label
    const dimText = `${Math.round(_cropRect.w)} × ${Math.round(_cropRect.h)}`;
    _ctx.font = '12px system-ui, sans-serif';
    _ctx.fillStyle = 'rgba(0,0,0,0.7)';
    const tw = _ctx.measureText(dimText).width;
    const labelY = cy + ch + 20 < _canvasH ? cy + ch + 6 : cy - 10;
    _ctx.fillRect(cx + cw / 2 - tw / 2 - 4, labelY - 2, tw + 8, 18);
    _ctx.fillStyle = '#fff';
    _ctx.textAlign = 'center';
    _ctx.fillText(dimText, cx + cw / 2, labelY + 12);
}

function canvasToImage(clientX, clientY) {
    const rect = _canvas.getBoundingClientRect();
    const canvasX = clientX - rect.left;
    const canvasY = clientY - rect.top;
    return {
        x: (canvasX - _offsetX) / _scale,
        y: (canvasY - _offsetY) / _scale
    };
}

function hitTestCropHandle(clientX, clientY) {
    if (!_cropRect) return null;

    const rect = _canvas.getBoundingClientRect();
    const mx = clientX - rect.left;
    const my = clientY - rect.top;

    const cx = _offsetX + _cropRect.x * _scale;
    const cy = _offsetY + _cropRect.y * _scale;
    const cw = _cropRect.w * _scale;
    const ch = _cropRect.h * _scale;
    const hs = HANDLE_SIZE + 4; // extra tolerance

    // Corner handles
    if (Math.abs(mx - cx) < hs && Math.abs(my - cy) < hs) return 'nw';
    if (Math.abs(mx - (cx + cw)) < hs && Math.abs(my - cy) < hs) return 'ne';
    if (Math.abs(mx - cx) < hs && Math.abs(my - (cy + ch)) < hs) return 'sw';
    if (Math.abs(mx - (cx + cw)) < hs && Math.abs(my - (cy + ch)) < hs) return 'se';

    // Edge handles
    if (Math.abs(mx - (cx + cw / 2)) < hs && Math.abs(my - cy) < hs) return 'n';
    if (Math.abs(mx - (cx + cw / 2)) < hs && Math.abs(my - (cy + ch)) < hs) return 's';
    if (Math.abs(mx - cx) < hs && Math.abs(my - (cy + ch / 2)) < hs) return 'w';
    if (Math.abs(mx - (cx + cw)) < hs && Math.abs(my - (cy + ch / 2)) < hs) return 'e';

    // Inside crop = move
    if (mx >= cx && mx <= cx + cw && my >= cy && my <= cy + ch) return 'move';

    return null;
}

function onMouseDown(e) {
    if (!_img) return;
    e.preventDefault();

    if (_cropActive) {
        const handle = _cropRect ? hitTestCropHandle(e.clientX, e.clientY) : null;
        if (handle) {
            _dragType = handle;
            _dragStart = { x: e.clientX, y: e.clientY };
            _cropStartRect = { ..._cropRect };
        } else {
            // Start new crop
            const pt = canvasToImage(e.clientX, e.clientY);
            _dragType = 'create';
            _dragStart = pt;
            _cropRect = { x: pt.x, y: pt.y, w: 0, h: 0 };
        }
    } else {
        // Pan mode
        _isPanning = true;
        _panStart = { x: e.clientX, y: e.clientY };
    }
}

function onMouseMove(e) {
    if (!_img) return;

    if (_cropActive && _cropRect && !_dragType) {
        // Update cursor based on handle hover
        const handle = hitTestCropHandle(e.clientX, e.clientY);
        _canvas.style.cursor = getCursorForHandle(handle);
        return;
    }

    if (_dragType) {
        e.preventDefault();
        const dim = getDisplayDimensions();

        if (_dragType === 'create') {
            const pt = canvasToImage(e.clientX, e.clientY);
            const x1 = Math.max(0, Math.min(_dragStart.x, pt.x));
            const y1 = Math.max(0, Math.min(_dragStart.y, pt.y));
            const x2 = Math.min(dim.w, Math.max(_dragStart.x, pt.x));
            const y2 = Math.min(dim.h, Math.max(_dragStart.y, pt.y));
            _cropRect = { x: x1, y: y1, w: x2 - x1, h: y2 - y1 };
        } else if (_dragType === 'move') {
            const dx = (e.clientX - _dragStart.x) / _scale;
            const dy = (e.clientY - _dragStart.y) / _scale;
            let nx = _cropStartRect.x + dx;
            let ny = _cropStartRect.y + dy;
            nx = Math.max(0, Math.min(dim.w - _cropStartRect.w, nx));
            ny = Math.max(0, Math.min(dim.h - _cropStartRect.h, ny));
            _cropRect = { x: nx, y: ny, w: _cropStartRect.w, h: _cropStartRect.h };
        } else {
            // Resize handles
            const dx = (e.clientX - _dragStart.x) / _scale;
            const dy = (e.clientY - _dragStart.y) / _scale;
            let { x, y, w, h } = _cropStartRect;

            if (_dragType.includes('w')) { x += dx; w -= dx; }
            if (_dragType.includes('e')) { w += dx; }
            if (_dragType.includes('n')) { y += dy; h -= dy; }
            if (_dragType.includes('s')) { h += dy; }

            // Enforce minimum size
            if (w < MIN_CROP) { w = MIN_CROP; if (_dragType.includes('w')) x = _cropStartRect.x + _cropStartRect.w - MIN_CROP; }
            if (h < MIN_CROP) { h = MIN_CROP; if (_dragType.includes('n')) y = _cropStartRect.y + _cropStartRect.h - MIN_CROP; }

            // Clamp to image bounds
            x = Math.max(0, x);
            y = Math.max(0, y);
            w = Math.min(dim.w - x, w);
            h = Math.min(dim.h - y, h);

            _cropRect = { x, y, w, h };
        }
        render();
    } else if (_isPanning) {
        const dx = e.clientX - _panStart.x;
        const dy = e.clientY - _panStart.y;
        _panX += dx;
        _panY += dy;
        _panStart = { x: e.clientX, y: e.clientY };
        render();
    }
}

function onMouseUp(e) {
    if (_dragType === 'create' && _cropRect && (_cropRect.w < MIN_CROP || _cropRect.h < MIN_CROP)) {
        _cropRect = null;
    }
    _dragType = null;
    _dragStart = null;
    _cropStartRect = null;
    _isPanning = false;
    _panStart = null;
    render();
}

function onWheel(e) {
    if (!_img) return;
    e.preventDefault();
    if (e.deltaY < 0) {
        _zoom = Math.min(_zoom * 1.1, 5.0);
    } else {
        _zoom = Math.max(_zoom / 1.1, 0.2);
    }
    fitImage();
    render();
}

// Touch support
let _lastTouchDist = null;

function onTouchStart(e) {
    if (!_img) return;
    e.preventDefault();

    if (e.touches.length === 2) {
        _lastTouchDist = getTouchDist(e.touches);
        return;
    }

    const touch = e.touches[0];
    onMouseDown({ clientX: touch.clientX, clientY: touch.clientY, preventDefault: () => {} });
}

function onTouchMove(e) {
    if (!_img) return;
    e.preventDefault();

    if (e.touches.length === 2 && _lastTouchDist !== null) {
        const dist = getTouchDist(e.touches);
        const ratio = dist / _lastTouchDist;
        _zoom = Math.max(0.2, Math.min(5.0, _zoom * ratio));
        _lastTouchDist = dist;
        fitImage();
        render();
        return;
    }

    const touch = e.touches[0];
    onMouseMove({ clientX: touch.clientX, clientY: touch.clientY, preventDefault: () => {} });
}

function onTouchEnd(e) {
    _lastTouchDist = null;
    onMouseUp(e);
}

function getTouchDist(touches) {
    const dx = touches[0].clientX - touches[1].clientX;
    const dy = touches[0].clientY - touches[1].clientY;
    return Math.sqrt(dx * dx + dy * dy);
}

function getCursorForHandle(handle) {
    switch (handle) {
        case 'nw': case 'se': return 'nwse-resize';
        case 'ne': case 'sw': return 'nesw-resize';
        case 'n': case 's': return 'ns-resize';
        case 'e': case 'w': return 'ew-resize';
        case 'move': return 'move';
        default: return 'crosshair';
    }
}
