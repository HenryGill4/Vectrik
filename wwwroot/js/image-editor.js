// image-editor.js — Canvas-based image crop/rotate/zoom editor.
// Loaded as an ES module from Blazor via IJSRuntime.
// Uses class-based instances so multiple editors can coexist on a page.

const HANDLE_SIZE = 8;
const MIN_CROP = 20;

class ImageEditorInstance {
    constructor(canvasId) {
        this.canvas = document.getElementById(canvasId);
        this.ctx = this.canvas ? this.canvas.getContext('2d') : null;
        this.img = null;

        // Transform
        this.rotation = 0;
        this.flipH = false;
        this.flipV = false;
        this.zoom = 1.0;
        this.panX = 0;
        this.panY = 0;

        // Crop
        this.cropActive = false;
        this.cropCircle = false;
        this.cropRect = null;
        this.dragType = null;
        this.dragStart = null;
        this.cropStartRect = null;

        // Canvas display
        this.canvasW = 0;
        this.canvasH = 0;
        this.scale = 1;
        this.offsetX = 0;
        this.offsetY = 0;

        // Pan
        this.isPanning = false;
        this.panStart = null;
        this.lastTouchDist = null;

        // Bind event handlers so we can remove them later
        this._onMouseDown = this.onMouseDown.bind(this);
        this._onMouseMove = this.onMouseMove.bind(this);
        this._onMouseUp = this.onMouseUp.bind(this);
        this._onWheel = this.onWheel.bind(this);
        this._onTouchStart = this.onTouchStart.bind(this);
        this._onTouchMove = this.onTouchMove.bind(this);
        this._onTouchEnd = this.onTouchEnd.bind(this);

        if (this.canvas) {
            this.canvas.addEventListener('mousedown', this._onMouseDown);
            this.canvas.addEventListener('mousemove', this._onMouseMove);
            this.canvas.addEventListener('mouseup', this._onMouseUp);
            this.canvas.addEventListener('mouseleave', this._onMouseUp);
            this.canvas.addEventListener('wheel', this._onWheel, { passive: false });
            this.canvas.addEventListener('touchstart', this._onTouchStart, { passive: false });
            this.canvas.addEventListener('touchmove', this._onTouchMove, { passive: false });
            this.canvas.addEventListener('touchend', this._onTouchEnd);
        }
    }

    loadImage(base64Data) {
        return new Promise((resolve) => {
            this.img = new Image();
            this.img.onload = () => {
                this.resetTransform();
                this.render();
                resolve(true);
            };
            this.img.onerror = () => resolve(false);
            this.img.src = base64Data;
        });
    }

    rotate(degrees) {
        this.rotation = ((this.rotation + degrees) % 360 + 360) % 360;
        this.cropRect = null;
        this.cropActive = false;
        this.fitImage();
        this.render();
    }

    flipHorizontal() {
        this.flipH = !this.flipH;
        this.render();
    }

    flipVertical() {
        this.flipV = !this.flipV;
        this.render();
    }

    zoomIn() {
        this.zoom = Math.min(this.zoom * 1.25, 5.0);
        this.fitImage();
        this.render();
    }

    zoomOut() {
        this.zoom = Math.max(this.zoom / 1.25, 0.2);
        this.fitImage();
        this.render();
    }

    zoomFit() {
        this.zoom = 1.0;
        this.panX = 0;
        this.panY = 0;
        this.fitImage();
        this.render();
    }

    toggleCrop() {
        this.cropActive = !this.cropActive;
        if (!this.cropActive) this.cropRect = null;
        this.render();
        return this.cropActive;
    }

    setCropCircle(isCircle) {
        this.cropCircle = isCircle;
        this.render();
    }

    resetAll() {
        this.rotation = 0;
        this.flipH = false;
        this.flipV = false;
        this.zoom = 1.0;
        this.panX = 0;
        this.panY = 0;
        this.cropRect = null;
        this.cropActive = false;
        this.cropCircle = false;
        this.fitImage();
        this.render();
    }

    exportImage(format, quality, circleClip) {
        if (!this.img) return null;

        const fmt = format || 'image/png';
        const q = quality || 0.92;

        // First, render the full transformed image to a temp canvas
        const rotated = this.rotation === 90 || this.rotation === 270;
        const fullW = rotated ? this.img.height : this.img.width;
        const fullH = rotated ? this.img.width : this.img.height;

        const tmpCanvas = document.createElement('canvas');
        tmpCanvas.width = fullW;
        tmpCanvas.height = fullH;
        const tmpCtx = tmpCanvas.getContext('2d');

        // Apply transforms: translate to center, rotate, flip, translate back
        tmpCtx.save();
        tmpCtx.translate(fullW / 2, fullH / 2);
        tmpCtx.rotate((this.rotation * Math.PI) / 180);
        tmpCtx.scale(this.flipH ? -1 : 1, this.flipV ? -1 : 1);
        tmpCtx.drawImage(this.img, -this.img.width / 2, -this.img.height / 2);
        tmpCtx.restore();

        // Now crop from the temp canvas
        let cropX = 0, cropY = 0, cropW = fullW, cropH = fullH;
        if (this.cropRect) {
            cropX = Math.max(0, Math.round(this.cropRect.x));
            cropY = Math.max(0, Math.round(this.cropRect.y));
            cropW = Math.min(fullW - cropX, Math.round(this.cropRect.w));
            cropH = Math.min(fullH - cropY, Math.round(this.cropRect.h));
        }

        const expCanvas = document.createElement('canvas');
        expCanvas.width = cropW;
        expCanvas.height = cropH;
        const expCtx = expCanvas.getContext('2d');

        // Circle clip
        if (circleClip && this.cropRect) {
            const r = Math.min(cropW, cropH) / 2;
            expCtx.beginPath();
            expCtx.arc(cropW / 2, cropH / 2, r, 0, Math.PI * 2);
            expCtx.closePath();
            expCtx.clip();
        }

        expCtx.drawImage(tmpCanvas, cropX, cropY, cropW, cropH, 0, 0, cropW, cropH);

        return expCanvas.toDataURL(fmt, q);
    }

    dispose() {
        if (this.canvas) {
            this.canvas.removeEventListener('mousedown', this._onMouseDown);
            this.canvas.removeEventListener('mousemove', this._onMouseMove);
            this.canvas.removeEventListener('mouseup', this._onMouseUp);
            this.canvas.removeEventListener('mouseleave', this._onMouseUp);
            this.canvas.removeEventListener('wheel', this._onWheel);
            this.canvas.removeEventListener('touchstart', this._onTouchStart);
            this.canvas.removeEventListener('touchmove', this._onTouchMove);
            this.canvas.removeEventListener('touchend', this._onTouchEnd);
        }
        this.canvas = null;
        this.ctx = null;
        this.img = null;
    }

    // ── Internal ─────────────────────────────────────────

    resetTransform() {
        this.rotation = 0;
        this.flipH = false;
        this.flipV = false;
        this.zoom = 1.0;
        this.panX = 0;
        this.panY = 0;
        this.cropRect = null;
        this.cropActive = false;
        this.fitImage();
    }

    getDisplayDimensions() {
        const r = this.rotation === 90 || this.rotation === 270;
        return { w: r ? this.img.height : this.img.width, h: r ? this.img.width : this.img.height };
    }

    fitImage() {
        if (!this.canvas || !this.img) return;

        const container = this.canvas.parentElement;
        this.canvasW = container.clientWidth || 600;
        this.canvasH = container.clientHeight || 400;
        this.canvas.width = this.canvasW;
        this.canvas.height = this.canvasH;

        const dim = this.getDisplayDimensions();
        const scaleX = this.canvasW / dim.w;
        const scaleY = this.canvasH / dim.h;
        this.scale = Math.min(scaleX, scaleY, 1) * this.zoom;

        this.offsetX = (this.canvasW - dim.w * this.scale) / 2 + this.panX;
        this.offsetY = (this.canvasH - dim.h * this.scale) / 2 + this.panY;
    }

    render() {
        if (!this.ctx || !this.img) return;
        this.fitImage();

        this.ctx.clearRect(0, 0, this.canvasW, this.canvasH);
        this.drawCheckerboard();

        const dim = this.getDisplayDimensions();

        this.ctx.save();
        this.ctx.translate(this.offsetX, this.offsetY);
        this.ctx.scale(this.scale, this.scale);

        // Apply transforms centered on image
        this.ctx.translate(dim.w / 2, dim.h / 2);
        this.ctx.rotate((this.rotation * Math.PI) / 180);
        this.ctx.scale(this.flipH ? -1 : 1, this.flipV ? -1 : 1);
        this.ctx.drawImage(this.img, -this.img.width / 2, -this.img.height / 2);
        this.ctx.restore();

        if (this.cropActive && this.cropRect) {
            this.drawCropOverlay();
        }
    }

    drawCheckerboard() {
        const size = 10;
        this.ctx.fillStyle = '#1a1a2e';
        this.ctx.fillRect(0, 0, this.canvasW, this.canvasH);
        this.ctx.fillStyle = '#22223a';
        for (let y = 0; y < this.canvasH; y += size * 2) {
            for (let x = 0; x < this.canvasW; x += size * 2) {
                this.ctx.fillRect(x, y, size, size);
                this.ctx.fillRect(x + size, y + size, size, size);
            }
        }
    }

    drawCropOverlay() {
        if (!this.cropRect) return;

        const cx = this.offsetX + this.cropRect.x * this.scale;
        const cy = this.offsetY + this.cropRect.y * this.scale;
        const cw = this.cropRect.w * this.scale;
        const ch = this.cropRect.h * this.scale;

        if (this.cropCircle) {
            // Circle crop overlay
            const r = Math.min(cw, ch) / 2;
            const centerX = cx + cw / 2;
            const centerY = cy + ch / 2;

            // Dim everything
            this.ctx.fillStyle = 'rgba(0, 0, 0, 0.55)';
            this.ctx.fillRect(0, 0, this.canvasW, this.canvasH);

            // Cut out the circle
            this.ctx.save();
            this.ctx.globalCompositeOperation = 'destination-out';
            this.ctx.beginPath();
            this.ctx.arc(centerX, centerY, r, 0, Math.PI * 2);
            this.ctx.fill();
            this.ctx.restore();

            // Circle border
            this.ctx.strokeStyle = '#fff';
            this.ctx.lineWidth = 2;
            this.ctx.beginPath();
            this.ctx.arc(centerX, centerY, r, 0, Math.PI * 2);
            this.ctx.stroke();

            // Crosshairs
            this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.25)';
            this.ctx.lineWidth = 1;
            this.ctx.beginPath();
            this.ctx.moveTo(centerX - r, centerY);
            this.ctx.lineTo(centerX + r, centerY);
            this.ctx.moveTo(centerX, centerY - r);
            this.ctx.lineTo(centerX, centerY + r);
            this.ctx.stroke();

            // Size label
            const diamText = `${Math.round(this.cropRect.w)}px circle`;
            this.ctx.font = '12px system-ui, sans-serif';
            this.ctx.fillStyle = 'rgba(0,0,0,0.7)';
            const tw = this.ctx.measureText(diamText).width;
            const labelY = centerY + r + 20 < this.canvasH ? centerY + r + 6 : centerY - r - 16;
            this.ctx.fillRect(centerX - tw / 2 - 4, labelY - 2, tw + 8, 18);
            this.ctx.fillStyle = '#fff';
            this.ctx.textAlign = 'center';
            this.ctx.fillText(diamText, centerX, labelY + 12);
        } else {
            // Rectangular crop overlay — dim outside
            this.ctx.fillStyle = 'rgba(0, 0, 0, 0.55)';
            this.ctx.fillRect(0, 0, this.canvasW, cy);
            this.ctx.fillRect(0, cy + ch, this.canvasW, this.canvasH - cy - ch);
            this.ctx.fillRect(0, cy, cx, ch);
            this.ctx.fillRect(cx + cw, cy, this.canvasW - cx - cw, ch);

            // Border
            this.ctx.strokeStyle = '#fff';
            this.ctx.lineWidth = 2;
            this.ctx.strokeRect(cx, cy, cw, ch);

            // Rule-of-thirds grid
            this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.3)';
            this.ctx.lineWidth = 1;
            for (let i = 1; i <= 2; i++) {
                this.ctx.beginPath();
                this.ctx.moveTo(cx + (cw * i) / 3, cy);
                this.ctx.lineTo(cx + (cw * i) / 3, cy + ch);
                this.ctx.stroke();
                this.ctx.beginPath();
                this.ctx.moveTo(cx, cy + (ch * i) / 3);
                this.ctx.lineTo(cx + cw, cy + (ch * i) / 3);
                this.ctx.stroke();
            }
        }

        // Corner handles (for both modes)
        const hs = HANDLE_SIZE;
        this.ctx.fillStyle = '#fff';
        [[cx - hs/2, cy - hs/2], [cx + cw - hs/2, cy - hs/2],
         [cx - hs/2, cy + ch - hs/2], [cx + cw - hs/2, cy + ch - hs/2]].forEach(([hx, hy]) => {
            this.ctx.fillRect(hx, hy, hs, hs);
        });

        // Edge handles
        [[cx + cw/2 - hs/2, cy - hs/2], [cx + cw/2 - hs/2, cy + ch - hs/2],
         [cx - hs/2, cy + ch/2 - hs/2], [cx + cw - hs/2, cy + ch/2 - hs/2]].forEach(([hx, hy]) => {
            this.ctx.fillRect(hx, hy, hs, hs);
        });

        // Dimensions label (rectangular only — circle shows above)
        if (!this.cropCircle) {
            const dimText = `${Math.round(this.cropRect.w)} \u00d7 ${Math.round(this.cropRect.h)}`;
            this.ctx.font = '12px system-ui, sans-serif';
            this.ctx.fillStyle = 'rgba(0,0,0,0.7)';
            const tw = this.ctx.measureText(dimText).width;
            const labelY = cy + ch + 20 < this.canvasH ? cy + ch + 6 : cy - 10;
            this.ctx.fillRect(cx + cw/2 - tw/2 - 4, labelY - 2, tw + 8, 18);
            this.ctx.fillStyle = '#fff';
            this.ctx.textAlign = 'center';
            this.ctx.fillText(dimText, cx + cw/2, labelY + 12);
        }
    }

    canvasToImage(clientX, clientY) {
        const rect = this.canvas.getBoundingClientRect();
        return {
            x: (clientX - rect.left - this.offsetX) / this.scale,
            y: (clientY - rect.top - this.offsetY) / this.scale
        };
    }

    hitTestCropHandle(clientX, clientY) {
        if (!this.cropRect) return null;

        const rect = this.canvas.getBoundingClientRect();
        const mx = clientX - rect.left;
        const my = clientY - rect.top;

        const cx = this.offsetX + this.cropRect.x * this.scale;
        const cy = this.offsetY + this.cropRect.y * this.scale;
        const cw = this.cropRect.w * this.scale;
        const ch = this.cropRect.h * this.scale;
        const hs = HANDLE_SIZE + 4;

        if (Math.abs(mx - cx) < hs && Math.abs(my - cy) < hs) return 'nw';
        if (Math.abs(mx - (cx + cw)) < hs && Math.abs(my - cy) < hs) return 'ne';
        if (Math.abs(mx - cx) < hs && Math.abs(my - (cy + ch)) < hs) return 'sw';
        if (Math.abs(mx - (cx + cw)) < hs && Math.abs(my - (cy + ch)) < hs) return 'se';
        if (Math.abs(mx - (cx + cw/2)) < hs && Math.abs(my - cy) < hs) return 'n';
        if (Math.abs(mx - (cx + cw/2)) < hs && Math.abs(my - (cy + ch)) < hs) return 's';
        if (Math.abs(mx - cx) < hs && Math.abs(my - (cy + ch/2)) < hs) return 'w';
        if (Math.abs(mx - (cx + cw)) < hs && Math.abs(my - (cy + ch/2)) < hs) return 'e';
        if (mx >= cx && mx <= cx + cw && my >= cy && my <= cy + ch) return 'move';

        return null;
    }

    // ── Event handlers ──

    onMouseDown(e) {
        if (!this.img) return;
        e.preventDefault();

        if (this.cropActive) {
            const handle = this.cropRect ? this.hitTestCropHandle(e.clientX, e.clientY) : null;
            if (handle) {
                this.dragType = handle;
                this.dragStart = { x: e.clientX, y: e.clientY };
                this.cropStartRect = { ...this.cropRect };
            } else {
                const pt = this.canvasToImage(e.clientX, e.clientY);
                this.dragType = 'create';
                this.dragStart = pt;
                this.cropRect = { x: pt.x, y: pt.y, w: 0, h: 0 };
            }
        } else {
            this.isPanning = true;
            this.panStart = { x: e.clientX, y: e.clientY };
        }
    }

    onMouseMove(e) {
        if (!this.img) return;

        if (this.cropActive && this.cropRect && !this.dragType) {
            const handle = this.hitTestCropHandle(e.clientX, e.clientY);
            this.canvas.style.cursor = getCursorForHandle(handle);
            return;
        }

        if (this.dragType) {
            e.preventDefault();
            const dim = this.getDisplayDimensions();

            if (this.dragType === 'create') {
                const pt = this.canvasToImage(e.clientX, e.clientY);
                let x1 = Math.max(0, Math.min(this.dragStart.x, pt.x));
                let y1 = Math.max(0, Math.min(this.dragStart.y, pt.y));
                let x2 = Math.min(dim.w, Math.max(this.dragStart.x, pt.x));
                let y2 = Math.min(dim.h, Math.max(this.dragStart.y, pt.y));

                // Enforce square for circle crop
                if (this.cropCircle) {
                    const side = Math.min(x2 - x1, y2 - y1);
                    if (pt.x < this.dragStart.x) x1 = x2 - side; else x2 = x1 + side;
                    if (pt.y < this.dragStart.y) y1 = y2 - side; else y2 = y1 + side;
                }

                this.cropRect = { x: x1, y: y1, w: x2 - x1, h: y2 - y1 };
            } else if (this.dragType === 'move') {
                const dx = (e.clientX - this.dragStart.x) / this.scale;
                const dy = (e.clientY - this.dragStart.y) / this.scale;
                let nx = this.cropStartRect.x + dx;
                let ny = this.cropStartRect.y + dy;
                nx = Math.max(0, Math.min(dim.w - this.cropStartRect.w, nx));
                ny = Math.max(0, Math.min(dim.h - this.cropStartRect.h, ny));
                this.cropRect = { x: nx, y: ny, w: this.cropStartRect.w, h: this.cropStartRect.h };
            } else {
                const dx = (e.clientX - this.dragStart.x) / this.scale;
                const dy = (e.clientY - this.dragStart.y) / this.scale;
                let { x, y, w, h } = this.cropStartRect;

                if (this.cropCircle) {
                    // For circle mode, resize proportionally (keep square)
                    let delta = dx;
                    if (this.dragType.includes('n') || this.dragType.includes('s')) delta = dy;
                    if (this.dragType.includes('w') || this.dragType.includes('n')) delta = -delta;

                    const newSize = Math.max(MIN_CROP, w + delta);
                    if (this.dragType.includes('w') || this.dragType === 'nw' || this.dragType === 'sw') x = x + w - newSize;
                    if (this.dragType.includes('n') || this.dragType === 'nw' || this.dragType === 'ne') y = y + h - newSize;
                    w = newSize;
                    h = newSize;
                } else {
                    if (this.dragType.includes('w')) { x += dx; w -= dx; }
                    if (this.dragType.includes('e')) { w += dx; }
                    if (this.dragType.includes('n')) { y += dy; h -= dy; }
                    if (this.dragType.includes('s')) { h += dy; }

                    if (w < MIN_CROP) { w = MIN_CROP; if (this.dragType.includes('w')) x = this.cropStartRect.x + this.cropStartRect.w - MIN_CROP; }
                    if (h < MIN_CROP) { h = MIN_CROP; if (this.dragType.includes('n')) y = this.cropStartRect.y + this.cropStartRect.h - MIN_CROP; }
                }

                x = Math.max(0, x);
                y = Math.max(0, y);
                w = Math.min(dim.w - x, w);
                h = Math.min(dim.h - y, h);

                this.cropRect = { x, y, w, h };
            }
            this.render();
        } else if (this.isPanning) {
            const dx = e.clientX - this.panStart.x;
            const dy = e.clientY - this.panStart.y;
            this.panX += dx;
            this.panY += dy;
            this.panStart = { x: e.clientX, y: e.clientY };
            this.render();
        }
    }

    onMouseUp() {
        if (this.dragType === 'create' && this.cropRect && (this.cropRect.w < MIN_CROP || this.cropRect.h < MIN_CROP)) {
            this.cropRect = null;
        }
        this.dragType = null;
        this.dragStart = null;
        this.cropStartRect = null;
        this.isPanning = false;
        this.panStart = null;
        this.render();
    }

    onWheel(e) {
        if (!this.img) return;
        e.preventDefault();
        this.zoom = e.deltaY < 0
            ? Math.min(this.zoom * 1.1, 5.0)
            : Math.max(this.zoom / 1.1, 0.2);
        this.fitImage();
        this.render();
    }

    onTouchStart(e) {
        if (!this.img) return;
        e.preventDefault();
        if (e.touches.length === 2) {
            this.lastTouchDist = getTouchDist(e.touches);
            return;
        }
        this.onMouseDown({ clientX: e.touches[0].clientX, clientY: e.touches[0].clientY, preventDefault: () => {} });
    }

    onTouchMove(e) {
        if (!this.img) return;
        e.preventDefault();
        if (e.touches.length === 2 && this.lastTouchDist !== null) {
            const dist = getTouchDist(e.touches);
            this.zoom = Math.max(0.2, Math.min(5.0, this.zoom * (dist / this.lastTouchDist)));
            this.lastTouchDist = dist;
            this.fitImage();
            this.render();
            return;
        }
        this.onMouseMove({ clientX: e.touches[0].clientX, clientY: e.touches[0].clientY, preventDefault: () => {} });
    }

    onTouchEnd() {
        this.lastTouchDist = null;
        this.onMouseUp();
    }
}

// ── Helpers ──

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

// ── Instance registry (keyed by canvas ID) ──
const _instances = new Map();

export function init(dotNetRef, canvasId) {
    // Dispose previous instance for this canvas if any
    if (_instances.has(canvasId)) {
        _instances.get(canvasId).dispose();
    }
    const inst = new ImageEditorInstance(canvasId);
    _instances.set(canvasId, inst);
}

export function loadImage(canvasId, base64Data) {
    const inst = _instances.get(canvasId);
    return inst ? inst.loadImage(base64Data) : Promise.resolve(false);
}

export function rotate(canvasId, degrees) {
    _instances.get(canvasId)?.rotate(degrees);
}

export function flipHorizontal(canvasId) {
    _instances.get(canvasId)?.flipHorizontal();
}

export function flipVertical(canvasId) {
    _instances.get(canvasId)?.flipVertical();
}

export function zoomIn(canvasId) {
    _instances.get(canvasId)?.zoomIn();
}

export function zoomOut(canvasId) {
    _instances.get(canvasId)?.zoomOut();
}

export function zoomFit(canvasId) {
    _instances.get(canvasId)?.zoomFit();
}

export function toggleCrop(canvasId) {
    const inst = _instances.get(canvasId);
    return inst ? inst.toggleCrop() : false;
}

export function setCropCircle(canvasId, isCircle) {
    _instances.get(canvasId)?.setCropCircle(isCircle);
}

export function resetAll(canvasId) {
    _instances.get(canvasId)?.resetAll();
}

export function exportImage(canvasId, format, quality, circleClip) {
    const inst = _instances.get(canvasId);
    return inst ? inst.exportImage(format, quality, circleClip) : null;
}

export function dispose(canvasId) {
    const inst = _instances.get(canvasId);
    if (inst) {
        inst.dispose();
        _instances.delete(canvasId);
    }
}
