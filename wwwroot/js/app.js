(() => {
    'use strict';

    // The root directory item is always seeded with id 1 by the server on startup.
    const ROOT_ID = 1;

    const els = {
        tree: document.getElementById('tree'),
        fileList: document.getElementById('file-list'),
        breadcrumb: document.getElementById('breadcrumb'),
        statusBar: document.getElementById('status-bar'),
        fileInput: document.getElementById('file-input'),
        btnNewFolder: document.getElementById('btn-new-folder'),
        btnUpload: document.getElementById('btn-upload'),
        btnCopy: document.getElementById('btn-copy'),
        btnCut: document.getElementById('btn-cut'),
        btnPaste: document.getElementById('btn-paste'),
        btnDownload: document.getElementById('btn-download'),
        btnDelete: document.getElementById('btn-delete'),
        searchInput: document.getElementById('search-input'),
        btnSearch: document.getElementById('btn-search'),
        btnRefresh: document.getElementById('btn-refresh'),
    };

    /** Map of item id -> tree node descriptor. */
    const treeNodes = new Map();

    const state = {
        currentNode: null,     // tree node descriptor for the folder shown in the list pane
        selectedEntry: null,   // { id, name, isDirectory } for the highlighted row in the list pane
        selectedRow: null,     // the DOM element for the highlighted row
        clipboard: null,       // { id, name, isDirectory, mode: 'copy' | 'cut', sourceParentId }
    };

    // ----------------------------------------------------------------- API --

    async function apiGet(url) {
        const res = await fetch(url);
        if (!res.ok) throw new Error(`GET ${url} failed: ${res.status}`);
        return res.json();
    }

    async function apiPost(url, body) {
        const res = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
        });
        if (!res.ok) throw new Error(`POST ${url} failed: ${res.status}`);
        return res.status === 204 ? null : res.json();
    }

    async function apiPutJson(url, body) {
        const res = await fetch(url, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
        });
        if (!res.ok) throw new Error(`PUT ${url} failed: ${res.status}`);
    }

    async function apiPutForm(url, formData) {
        const res = await fetch(url, { method: 'PUT', body: formData });
        if (!res.ok) throw new Error(`PUT ${url} failed: ${res.status}`);
    }

    async function apiDelete(url) {
        const res = await fetch(url, { method: 'DELETE' });
        if (!res.ok) throw new Error(`DELETE ${url} failed: ${res.status}`);
    }

    // --------------------------------------------------------------- Utils --

    function formatSize(bytes) {
        if (bytes === null || bytes === undefined) return '';
        if (bytes < 1024) return `${bytes} B`;
        const units = ['KB', 'MB', 'GB', 'TB'];
        let value = bytes;
        let unit = -1;
        do {
            value /= 1024;
            unit++;
        } while (value >= 1024 && unit < units.length - 1);
        return `${value.toFixed(1)} ${units[unit]}`;
    }

    function displayName(item) {
        return item.path === '/' ? 'Root' : item.name;
    }

    function setStatus(message) {
        els.statusBar.textContent = message || '';
    }

    function showError(err) {
        console.error(err);
        setStatus(err.message || String(err));
    }

    function svgIcon(symbolId, className) {
        const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('class', className);
        const use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
        use.setAttribute('href', `#${symbolId}`);
        svg.appendChild(use);
        return svg;
    }

    // ---------------------------------------------------------------- Tree --

    function createTreeNode(item, parentNode) {
        const li = document.createElement('li');

        const row = document.createElement('div');
        row.className = 'node';

        const twisty = svgIcon('icon-chevron', 'twisty leaf');
        const icon = svgIcon('icon-folder', 'node-icon');

        const label = document.createElement('span');
        label.className = 'node-label';
        label.textContent = displayName(item);

        row.append(twisty, icon, label);
        li.appendChild(row);

        const childrenUl = document.createElement('ul');
        childrenUl.style.display = 'none';
        li.appendChild(childrenUl);

        const node = {
            id: item.id,
            item,
            parent: parentNode || null,
            li,
            row,
            twisty,
            childrenUl,
            loaded: false,
            expanded: false,
        };

        treeNodes.set(item.id, node);

        twisty.addEventListener('click', (e) => {
            e.stopPropagation();
            toggleNode(node);
        });

        row.addEventListener('click', () => selectTreeNode(node));

        row.addEventListener('dragover', (e) => {
            e.preventDefault();
            row.classList.add('drop-target');
        });
        row.addEventListener('dragleave', () => row.classList.remove('drop-target'));
        row.addEventListener('drop', (e) => {
            e.preventDefault();
            row.classList.remove('drop-target');
            handleDrop(node.item.id, e.dataTransfer.getData('text/plain'));
        });

        return node;
    }

    async function loadChildren(node) {
        const entries = await apiGet(`/api/storage/${node.id}/children`);
        node.childrenUl.innerHTML = '';

        const directories = entries
            .filter((e) => e.item.isDirectory)
            .sort((a, b) => a.item.name.localeCompare(b.item.name));

        for (const entry of directories) {
            const childNode = createTreeNode(entry.item, node);
            node.childrenUl.appendChild(childNode.li);
        }

        node.twisty.classList.toggle('leaf', directories.length === 0);
        node.loaded = true;
    }

    async function toggleNode(node) {
        if (!node.loaded) {
            await loadChildren(node);
        }

        node.expanded = !node.expanded;
        node.childrenUl.style.display = node.expanded ? '' : 'none';
        node.twisty.classList.toggle('expanded', node.expanded);
    }

    async function expandNode(node) {
        if (!node.loaded) {
            await loadChildren(node);
        }
        if (!node.expanded) {
            node.expanded = true;
            node.childrenUl.style.display = '';
            node.twisty.classList.add('expanded');
        }
    }

    async function selectTreeNode(node) {
        if (state.currentNode) {
            state.currentNode.row.classList.remove('selected');
        }
        node.row.classList.add('selected');
        state.currentNode = node;

        await expandNode(node);
        await refreshList();
        renderBreadcrumb(node);
    }

    /** Navigates to the folder with the given id, expanding the tree as needed. */
    async function navigateTo(id) {
        let node = treeNodes.get(id);
        if (!node) {
            const parent = state.currentNode;
            if (parent) {
                await expandNode(parent);
                node = treeNodes.get(id);
            }
        }
        if (node) {
            await selectTreeNode(node);
        }
    }

    async function initTree() {
        const root = await apiGet(`/api/storage/${ROOT_ID}`);
        const node = createTreeNode(root, null);
        els.tree.appendChild(node.li);
        await selectTreeNode(node);
    }

    /** Navigates to the directory at the given path, expanding the tree as needed from the root. */
    async function navigateToPath(item) {
        const path = typeof item === 'string' ? item : item.path;
        if (path === '/') {
            await selectTreeNode(treeNodes.get(ROOT_ID));
            return;
        }

        const segments = path.split('/').filter((s) => s.length > 0);
        let node = treeNodes.get(ROOT_ID);
        for (const segment of segments) {
            await expandNode(node);
            const child = Array.from(treeNodes.values()).find((n) => n.parent === node && n.item.name === segment);
            if (!child) {
                showError(new Error(`Could not locate "${path}".`));
                return;
            }
            node = child;
        }

        await selectTreeNode(node);
    }

    // ------------------------------------------------------------ Breadcrumb --

    function renderBreadcrumb(node) {
        const chain = [];
        for (let n = node; n; n = n.parent) chain.unshift(n);

        els.breadcrumb.innerHTML = '';
        chain.forEach((n, index) => {
            if (index > 0) {
                const sep = document.createElement('span');
                sep.className = 'sep';
                sep.textContent = '›';
                els.breadcrumb.appendChild(sep);
            }

            const crumb = document.createElement('span');
            crumb.className = 'crumb';
            crumb.textContent = displayName(n.item);
            if (n === node) {
                crumb.classList.add('current');
            } else {
                crumb.addEventListener('click', () => selectTreeNode(n));
            }
            els.breadcrumb.appendChild(crumb);
        });
    }

    // ------------------------------------------------------------- File list --

    function clearSelection() {
        if (state.selectedRow) {
            state.selectedRow.classList.remove('selected');
        }
        state.selectedRow = null;
        state.selectedEntry = null;
        updateToolbar();
    }

    function selectRow(row, entry) {
        if (state.selectedRow) {
            state.selectedRow.classList.remove('selected');
        }
        row.classList.add('selected');
        state.selectedRow = row;
        state.selectedEntry = entry;
        updateToolbar();
    }

    function updateToolbar() {
        const hasSelection = !!state.selectedEntry;
        els.btnCopy.disabled = !hasSelection;
        els.btnCut.disabled = !hasSelection;
        els.btnDelete.disabled = !hasSelection;
        els.btnDownload.disabled = !hasSelection || state.selectedEntry.isDirectory;
        els.btnPaste.disabled = !state.clipboard;
    }

    function createRow(item, fileCount, options = {}) {
        const { showPath = false, onOpenDirectory } = options;

        const row = document.createElement('div');
        row.className = 'file-row';
        row.draggable = true;

        const icon = svgIcon(item.isDirectory ? 'icon-folder' : 'icon-file', 'row-icon');

        const name = document.createElement('span');
        name.className = 'col-name';
        name.textContent = item.name;

        if (showPath) {
            const path = document.createElement('span');
            path.className = 'col-path';
            path.textContent = item.path;
            name.appendChild(path);
        }

        const detail = document.createElement('span');
        detail.className = 'col-detail';
        detail.textContent = item.isDirectory ? `(${fileCount})` : formatSize(item.size);

        row.append(icon, name, detail);

        row.addEventListener('click', () => selectRow(row, item));

        if (item.isDirectory) {
            row.addEventListener('dblclick', () => (onOpenDirectory || navigateToPath)(item));

            row.addEventListener('dragover', (e) => {
                e.preventDefault();
                row.classList.add('drop-target');
            });
            row.addEventListener('dragleave', () => row.classList.remove('drop-target'));
            row.addEventListener('drop', (e) => {
                e.preventDefault();
                row.classList.remove('drop-target');
                handleDrop(item.id, e.dataTransfer.getData('text/plain'));
            });
        } else {
            row.addEventListener('dblclick', () => downloadItem(item));
        }

        row.addEventListener('dragstart', (e) => {
            e.dataTransfer.setData('text/plain', String(item.id));
            e.dataTransfer.effectAllowed = 'move';
        });

        if (state.clipboard && state.clipboard.mode === 'cut' && state.clipboard.id === item.id) {
            row.classList.add('cut');
        }

        return row;
    }

    function createFileRow(entry) {
        return createRow(entry.item, entry.fileCount, { onOpenDirectory: (item) => navigateTo(item.id) });
    }

    async function refreshList() {
        const node = state.currentNode;
        if (!node) return;

        state.searchMode = false;
        clearSelection();
        const entries = await apiGet(`/api/storage/${node.id}/children`);

        entries.sort((a, b) => {
            if (a.item.isDirectory !== b.item.isDirectory) return a.item.isDirectory ? -1 : 1;
            return a.item.name.localeCompare(b.item.name);
        });

        els.fileList.innerHTML = '';
        if (entries.length === 0) {
            const empty = document.createElement('div');
            empty.className = 'empty-message';
            empty.textContent = 'This folder is empty.';
            els.fileList.appendChild(empty);
            return;
        }

        for (const entry of entries) {
            els.fileList.appendChild(createFileRow(entry));
        }
    }

    /** Reloads the tree children of a folder (if loaded) and the list pane (if it's the current folder). */
    async function refreshFolder(id) {
        const node = treeNodes.get(id);
        if (node && node.loaded) {
            const wasExpanded = node.expanded;
            node.loaded = false;
            await loadChildren(node);
            if (wasExpanded) {
                node.expanded = true;
                node.childrenUl.style.display = '';
                node.twisty.classList.add('expanded');
            }
        }

        if (state.currentNode && state.currentNode.id === id) {
            await refreshList();
        }
    }

    function removeTreeNode(id) {
        const node = treeNodes.get(id);
        if (!node) return;

        const isDescendant = (n) => {
            for (let p = n.parent; p; p = p.parent) {
                if (p === node) return true;
            }
            return false;
        };
        for (const [otherId, other] of Array.from(treeNodes.entries())) {
            if (other === node || isDescendant(other)) {
                treeNodes.delete(otherId);
            }
        }

        node.li.remove();

        if (state.currentNode === node) {
            navigateTo(node.parent ? node.parent.id : ROOT_ID);
        }
    }

    // ---------------------------------------------------------------- Actions --

    async function newFolder() {
        const node = state.currentNode;
        if (!node) return;

        const name = prompt('Folder name:');
        if (!name) return;

        try {
            await apiPost('/api/storage', { parentId: node.id, name, isDirectory: true });
            await refreshFolder(node.id);
            setStatus(`Created folder "${name}".`);
        } catch (err) {
            showError(err);
        }
    }

    function upload() {
        if (!state.currentNode) return;
        els.fileInput.click();
    }

    async function handleFileInputChange() {
        const node = state.currentNode;
        const files = Array.from(els.fileInput.files);
        els.fileInput.value = '';
        if (!node || files.length === 0) return;

        try {
            for (const file of files) {
                const item = await apiPost('/api/storage', {
                    parentId: node.id,
                    name: file.name,
                    isDirectory: false,
                });

                const formData = new FormData();
                formData.append('file', file);
                await apiPutForm(`/api/storage/${item.id}/content`, formData);
            }

            await refreshFolder(node.id);
            setStatus(`Uploaded ${files.length} file(s).`);
        } catch (err) {
            showError(err);
        }
    }

    function downloadItem(item) {
        const a = document.createElement('a');
        a.href = `/api/storage/${item.id}/content`;
        a.download = item.name;
        document.body.appendChild(a);
        a.click();
        a.remove();
    }

    async function search() {
        const term = els.searchInput.value.trim();
        if (!term) return;

        let scopePath = null;
        let scopeLabel = 'entire hierarchy';
        if (state.selectedEntry && state.selectedEntry.isDirectory) {
            scopePath = state.selectedEntry.path;
            scopeLabel = `"${displayName(state.selectedEntry)}"`;
        }

        try {
            const params = new URLSearchParams({ name: term });
            if (scopePath) params.set('path', scopePath);

            const results = await apiGet(`/api/search?${params.toString()}`);
            await renderSearchResults(term, scopeLabel, results);
            setStatus(`${results.length} result(s) for "${term}" in ${scopeLabel}.`);
        } catch (err) {
            showError(err);
        }
    }

    async function renderSearchResults(term, scopeLabel, items) {
        clearSelection();
        state.searchMode = true;
        els.fileList.innerHTML = '';

        const header = document.createElement('div');
        header.className = 'search-header';
        header.textContent = `${items.length} result(s) for "${term}" in ${scopeLabel}`;

        const clearButton = document.createElement('button');
        clearButton.type = 'button';
        clearButton.className = 'clear-search';
        clearButton.textContent = 'Clear';
        clearButton.addEventListener('click', () => refreshList());
        header.appendChild(clearButton);

        els.fileList.appendChild(header);

        if (items.length === 0) {
            const empty = document.createElement('div');
            empty.className = 'empty-message';
            empty.textContent = 'No matches found.';
            els.fileList.appendChild(empty);
            return;
        }

        const sorted = [...items].sort((a, b) => {
            if (a.isDirectory !== b.isDirectory) return a.isDirectory ? -1 : 1;
            return a.name.localeCompare(b.name);
        });

        for (const item of sorted) {
            let fileCount = null;
            if (item.isDirectory) {
                const entry = await apiGet(`/api/storage/${item.id}/entry`);
                fileCount = entry.fileCount;
            }
            els.fileList.appendChild(createRow(item, fileCount, { showPath: true }));
        }
    }

    function copySelection() {
        if (!state.selectedEntry || !state.currentNode) return;
        state.clipboard = {
            id: state.selectedEntry.id,
            name: state.selectedEntry.name,
            isDirectory: state.selectedEntry.isDirectory,
            mode: 'copy',
            sourceParentId: state.currentNode.id,
        };
        updateToolbar();
        setStatus(`Copied "${state.selectedEntry.name}".`);
    }

    function cutSelection() {
        if (!state.selectedEntry || !state.currentNode) return;
        state.clipboard = {
            id: state.selectedEntry.id,
            name: state.selectedEntry.name,
            isDirectory: state.selectedEntry.isDirectory,
            mode: 'cut',
            sourceParentId: state.currentNode.id,
        };
        updateToolbar();
        refreshList();
        setStatus(`Cut "${state.selectedEntry.name}".`);
    }

    /** Picks a non-colliding name for a copy placed into the given folder. */
    async function uniqueNameFor(targetFolderId, baseName) {
        const entries = await apiGet(`/api/storage/${targetFolderId}/children`);
        const existing = new Set(entries.map((e) => e.item.name));
        if (!existing.has(baseName)) return baseName;

        const dot = baseName.lastIndexOf('.');
        const stem = dot > 0 ? baseName.slice(0, dot) : baseName;
        const ext = dot > 0 ? baseName.slice(dot) : '';

        let candidate;
        let n = 2;
        do {
            candidate = `${stem} - Copy${n === 2 ? '' : ` (${n})`}${ext}`;
            n++;
        } while (existing.has(candidate));

        return candidate;
    }

    async function paste() {
        const clip = state.clipboard;
        const target = state.currentNode;
        if (!clip || !target) return;

        try {
            if (clip.mode === 'cut') {
                await apiPutJson(`/api/storage/${clip.id}/move`, { parentId: target.id });
                await refreshFolder(clip.sourceParentId);
                await refreshFolder(target.id);
                state.clipboard = null;
                setStatus(`Moved "${clip.name}".`);
            } else {
                const name = await uniqueNameFor(target.id, clip.name);
                await apiPost(`/api/storage/${clip.id}/copy`, { parentId: target.id, name });
                await refreshFolder(target.id);
                setStatus(`Copied "${clip.name}" as "${name}".`);
            }
        } catch (err) {
            showError(err);
        } finally {
            updateToolbar();
        }
    }

    async function deleteSelection() {
        const entry = state.selectedEntry;
        const node = state.currentNode;
        if (!entry || !node) return;

        if (!confirm(`Delete "${entry.name}"?`)) return;

        try {
            await apiDelete(`/api/storage/${entry.id}`);
            if (entry.isDirectory) {
                removeTreeNode(entry.id);
            }
            if (state.clipboard && state.clipboard.id === entry.id) {
                state.clipboard = null;
            }
            await refreshFolder(node.id);
            setStatus(`Deleted "${entry.name}".`);
        } catch (err) {
            showError(err);
        }
    }

    async function handleDrop(targetId, draggedIdText) {
        if (!draggedIdText) return;

        const draggedId = Number(draggedIdText);
        if (draggedId === targetId) return;

        const sourceNode = state.currentNode;

        try {
            await apiPutJson(`/api/storage/${draggedId}/move`, { parentId: targetId });
            if (sourceNode) await refreshFolder(sourceNode.id);
            await refreshFolder(targetId);
            setStatus('Moved item.');
        } catch (err) {
            showError(err);
        }
    }

    async function refresh() {
        if (!state.currentNode) return;
        await refreshFolder(state.currentNode.id);
        setStatus('Refreshed.');
    }

    // ----------------------------------------------------------------- Init --

    function wireUp() {
        els.btnNewFolder.addEventListener('click', newFolder);
        els.btnUpload.addEventListener('click', upload);
        els.fileInput.addEventListener('change', handleFileInputChange);
        els.btnCopy.addEventListener('click', copySelection);
        els.btnCut.addEventListener('click', cutSelection);
        els.btnPaste.addEventListener('click', paste);
        els.btnDownload.addEventListener('click', () => state.selectedEntry && downloadItem(state.selectedEntry));
        els.btnDelete.addEventListener('click', deleteSelection);
        els.btnSearch.addEventListener('click', search);
        els.btnRefresh.addEventListener('click', refresh);
    }

    wireUp();
    initTree().catch(showError);
})();
