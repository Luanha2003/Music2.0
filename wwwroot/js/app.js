/* ============================================================
   app.js - SPA Router & Page Renderers
   Ứng dụng nghe nhạc Music2.0
   ============================================================ */
const ytapi_key = "AIzaSyAXINrJXMZzOgZlQhnhZmgxM47zBnA2Ejs";
const App = {
    pageContent: null,
    currentPage: null,
    _searchTimeout: null,
    _bannerInterval: null,
    _bannerIndex: 0,



    init() {
        this.pageContent = document.getElementById('page-content');
        Player.init();

        // Navigation
        document.querySelectorAll('.nav-item[data-page]').forEach(item => {
            item.addEventListener('click', () => {
                const page = item.dataset.page;
                const targetHash = page === 'home' ? '#/' : '#/' + page;

                // Hide mobile menu if open
                if (this.sidebar && this.sidebar.classList.contains('open')) {
                    this.sidebar.classList.remove('open');
                    if (this.sidebarOverlay) this.sidebarOverlay.classList.remove('show');
                }

                if (location.hash === targetHash || (page === 'home' && (location.hash === '' || location.hash === '#/home'))) {
                    this.navigate(page); // Reload if clicking current page
                } else {
                    location.hash = targetHash;
                }
            });
        });

        // Hash routing
        window.addEventListener('hashchange', () => this._handleHash());

        // Mobile Menu Toggle
        this.btnMenu = document.getElementById('btn-mobile-menu');
        this.sidebarOverlay = document.getElementById('sidebar-overlay');
        this.sidebar = document.querySelector('.sidebar');

        if (this.btnMenu && this.sidebarOverlay && this.sidebar) {
            this.btnMenu.addEventListener('click', () => {
                this.sidebar.classList.add('open');
                this.sidebarOverlay.classList.add('show');
            });
            this.sidebarOverlay.addEventListener('click', () => {
                this.sidebar.classList.remove('open');
                this.sidebarOverlay.classList.remove('show');
            });
        }

        // PC Menu Toggle
        this.btnPcMenu = document.getElementById('btn-pc-menu');
        this.appContainer = document.querySelector('.app-container');
        if (this.btnPcMenu && this.appContainer) {
            this.btnPcMenu.addEventListener('click', () => {
                this.appContainer.classList.toggle('collapsed');
            });
        }

        // Initial load
        this._handleHash();
    },

    // ── Navigation ──
    navigate(page, params = {}) {
        // Update nav active state
        document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));
        const navItem = document.querySelector(`.nav-item[data-page="${page}"]`);
        if (navItem) navItem.classList.add('active');

        this.currentPage = page;
        this.pageContent.scrollTop = 0;
        document.getElementById('main-content').scrollTop = 0;

        switch (page) {
            case 'home': this.loadHome(); break;
            case 'search': this.loadSearch(params.q); break;
            case 'chart': this.loadChart(); break;
            case 'newrelease': this.loadNewRelease(); break;
            case 'top100': this.loadTop100(); break;
            case 'library': this.loadLibrary(); break;
            case 'myplaylist': this.loadMyPlaylist(); break;
            case 'artist': this.loadArtist(params.alias); break;
            case 'playlist': this.loadPlaylist(params.id); break;
            case 'karaoke': this.loadKaraoke(); break;
        }
    }, _handleHash() {
        if (!location.hash || location.hash === '#/' || location.hash === '#/home') {
            this.navigate('home');
            return;
        }

        const hash = location.hash.slice(1);
        if (hash.startsWith('/myplaylist/')) {
            this.navigate('myplaylist', { id: hash.replace('/myplaylist/', '') });
            return;
        }
        if (hash.startsWith('/artist/')) {
            this.navigate('artist', { alias: hash.replace('/artist/', '') });
        } else if (hash.startsWith('/playlist/')) {
            this.navigate('playlist', { id: hash.replace('/playlist/', '') });
        } else if (hash.startsWith('/search/')) {
            this.navigate('search', { q: decodeURIComponent(hash.replace('/search/', '')) });
        } else if (hash === '/library') {
            this.navigate('library');
        } else if (hash === '/myplaylist') {
            this.navigate('myplaylist');
        } else if (hash === '/chart') {
            this.navigate('chart');
        } else if (hash === '/top100') {
            this.navigate('top100');
        } else if (hash === '/newrelease') {
            this.navigate('newrelease');
        } else if (hash === '/karaoke') {
            this.navigate('karaoke');
        }
    },

    // ── API Helper ──
    async fetchApi(url) {
        try {
            const res = await fetch(url);
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            return await res.json();
        } catch (err) {
            console.error(`API Error [${url}]:`, err);
            return null;
        }
    },

    // ── Toast ──
    showToast(msg) {
        const toast = document.getElementById('toast');
        toast.textContent = msg;
        toast.classList.add('show');
        setTimeout(() => toast.classList.remove('show'), 2500);
    },

    // ── Loading ──
    showLoading() {
        this.pageContent.innerHTML = '<div class="loading-spinner"><div class="spinner"></div></div>';
    },

    // ═══════════════════════════════════
    // TRANG CHỦ
    // ═══════════════════════════════════
    async loadHome() {
        this.showLoading();
        const json = await this.fetchApi('/api/home');

        if (!json || json.err !== 0) {
            this.pageContent.innerHTML = '<p style="text-align:center;padding:60px;color:var(--text-muted)">Không thể tải trang chủ. Vui lòng thử lại.</p>';
            return;
        }

        const data = json.data;
        let html = '<div class="fade-in">';

        // Parse sections from home data
        if (data.items) {
            for (const section of data.items) {
                if (section.sectionType === 'banner') {
                    html += this._renderBanner(section.items);
                } else if (section.sectionType === 'new-release') {
                    html += this._renderNewReleaseSection(section);
                } else if (section.sectionType === 'playlist') {
                    html += this._renderPlaylistSection(section);
                } else if (section.sectionType === 'newReleaseChart' || section.title?.includes('BXH') || section.title?.includes('Chart')) {
                    html += this._renderChartPreviewSection(section);
                }
            }
        }

        html += '</div>';
        this.pageContent.innerHTML = html;

        // Start banner auto-slide
        this._startBannerSlider();
        // Bind card clicks
        this._bindCardClicks();
    },

    _renderBanner(items) {
        if (!items || items.length === 0) return '';
        const slides = items.slice(0, 6);
        let html = '<div class="hero-banner">';
        slides.forEach((item, i) => {
            html += `
                <div class="hero-slide ${i === 0 ? 'active' : ''}" data-index="${i}"
                     data-type="${item.type}" data-id="${item.encodeId || ''}" data-link="${item.link || ''}">
                    <img src="${item.banner || item.thumbnailM || item.thumbnail || '/music.png'}" alt="" />
                    <div class="hero-overlay"></div>
                    <div class="hero-info">
                        <span class="hero-tag">${item.type === 4 ? 'Playlist' : 'Nổi bật'}</span>
                        <div class="hero-title">${item.title || ''}</div>
                        <div class="hero-artist">${item.artistsNames || ''}</div>
                    </div>
                </div>`;
        });
        html += '<div class="hero-dots">';
        slides.forEach((_, i) => {
            html += `<div class="hero-dot ${i === 0 ? 'active' : ''}" data-slide="${i}"></div>`;
        });
        html += '</div></div>';
        return html;
    },

    _startBannerSlider() {
        if (this._bannerInterval) clearInterval(this._bannerInterval);
        this._bannerIndex = 0;

        // Dot clicks
        document.querySelectorAll('.hero-dot').forEach(dot => {
            dot.addEventListener('click', () => {
                this._bannerIndex = parseInt(dot.dataset.slide);
                this._updateBanner();
            });
        });

        // Slide clicks
        document.querySelectorAll('.hero-slide').forEach(slide => {
            slide.addEventListener('click', () => {
                const link = slide.dataset.link;
                if (link) {
                    // Parse link to extract playlist/album id
                    const match = link.match(/\/([A-Z0-9]{8})\.html/);
                    if (match) {
                        location.hash = `/playlist/${match[1]}`;
                    }
                }
            });
        });

        this._bannerInterval = setInterval(() => {
            const slides = document.querySelectorAll('.hero-slide');
            if (slides.length === 0) { clearInterval(this._bannerInterval); return; }
            this._bannerIndex = (this._bannerIndex + 1) % slides.length;
            this._updateBanner();
        }, 5000);
    },

    _updateBanner() {
        document.querySelectorAll('.hero-slide').forEach(s => s.classList.remove('active'));
        document.querySelectorAll('.hero-dot').forEach(d => d.classList.remove('active'));
        const slide = document.querySelector(`.hero-slide[data-index="${this._bannerIndex}"]`);
        const dot = document.querySelector(`.hero-dot[data-slide="${this._bannerIndex}"]`);
        if (slide) slide.classList.add('active');
        if (dot) dot.classList.add('active');
    },

    _renderNewReleaseSection(section) {
        if (!section.items) return '';
        let html = `<div class="section">
            <div class="section-header">
                <h2 class="section-title">${section.title || 'Mới Phát Hành'}</h2>
            </div>
            <div class="song-list stagger">`;

        const songs = (section.items.all || section.items.vPop || section.items.others || section.items || []).slice(0, 10);
        songs.forEach((song, i) => {
            html += this._renderSongItem(song, i + 1, songs);
        });

        html += '</div></div>';
        return html;
    },

    _renderPlaylistSection(section) {
        if (!section.items || section.items.length === 0) return '';
        let html = `<div class="section">
            <div class="section-header">
                <h2 class="section-title">${section.title || 'Playlist'}</h2>
            </div>
            <div class="card-grid stagger">`;

        section.items.slice(0, 8).forEach(item => {
            html += this._renderCard(item);
        });

        html += '</div></div>';
        return html;
    },

    _renderChartPreviewSection(section) {
        if (!section.items || section.items.length === 0) return '';
        let html = `<div class="section">
            <div class="section-header">
                <h2 class="section-title">${section.title || '#zingchart'}</h2>
                <a class="section-link" href="javascript:void(0)" onclick="App.navigate('chart')">Xem tất cả</a>
            </div>
            <div class="song-list stagger">`;

        section.items.slice(0, 5).forEach((song, i) => {
            html += this._renderChartItem(song, i + 1);
        });

        html += '</div></div>';
        return html;
    },

    // ═══════════════════════════════════
    // BẢNG XẾP HẠNG (#ZINGCHART)
    // ═══════════════════════════════════
    async loadChart() {
        this.showLoading();
        const json = await this.fetchApi('/api/chart');

        if (!json || json.err !== 0) {
            this.pageContent.innerHTML = '<p style="text-align:center;padding:60px;color:var(--text-muted)">Không thể tải bảng xếp hạng</p>';
            return;
        }

        const data = json.data;
        let html = '<div class="fade-in">';
        html += '<div class="section-header"><h2 class="section-title" style="font-size:2rem">#zingchart</h2></div>';

        if (data.RTChart && data.RTChart.items) {
            html += '<div class="song-list stagger">';
            data.RTChart.items.slice(0, 20).forEach((song, i) => {
                html += this._renderChartItem(song, i + 1);
            });
            html += '</div>';
        }

        html += '</div>';
        this.pageContent.innerHTML = html;
        this._bindSongClicks();
    },

    // ═══════════════════════════════════
    // NHẠC MỚI
    // ═══════════════════════════════════
    async loadNewRelease() {
        this.showLoading();
        const json = await this.fetchApi('/api/newrelease');

        if (!json || json.err !== 0) {
            this.pageContent.innerHTML = '<p style="text-align:center;padding:60px;color:var(--text-muted)">Không thể tải nhạc mới</p>';
            return;
        }

        let html = '<div class="fade-in">';
        html += '<div class="section-header"><h2 class="section-title" style="font-size:2rem">Nhạc Mới Phát Hành</h2></div>';

        const songs = json.data.items || json.data || [];
        html += '<div class="song-list stagger">';
        songs.slice(0, 30).forEach((song, i) => {
            html += this._renderSongItem(song, i + 1, songs);
        });
        html += '</div></div>';

        this.pageContent.innerHTML = html;
        this._bindSongClicks();
    },

    // ═══════════════════════════════════
    // TOP 100
    // ═══════════════════════════════════
    async loadTop100() {
        this.showLoading();
        const json = await this.fetchApi('/api/top100');

        if (!json || json.err !== 0) {
            this.pageContent.innerHTML = '<p style="text-align:center;padding:60px;color:var(--text-muted)">Không thể tải Top 100</p>';
            return;
        }

        let html = '<div class="fade-in">';
        html += '<div class="section-header"><h2 class="section-title" style="font-size:2rem">Top 100</h2></div>';

        const groups = json.data || [];
        groups.forEach(group => {
            if (group.items && group.items.length > 0) {
                html += `<div class="section">
                    <div class="section-header"><h2 class="section-title">${group.title || ''}</h2></div>
                    <div class="card-grid stagger">`;
                group.items.slice(0, 8).forEach(item => {
                    html += this._renderCard(item);
                });
                html += '</div></div>';
            }
        });

        html += '</div>';
        this.pageContent.innerHTML = html;
        this._bindCardClicks();
    },

    // ═══════════════════════════════════
    // TÌM KIẾM
    // ═══════════════════════════════════
    async loadSearch(initialQuery) {
        let html = '<div class="fade-in">';
        html += `<div class="search-wrapper">
            <span class="search-icon"><svg viewBox="0 0 24 24" fill="currentColor"><path d="M10.533 1.279c-5.18 0-9.407 4.14-9.407 9.279s4.226 9.279 9.407 9.279c2.234 0 4.29-.77 5.907-2.058l4.353 4.353a1 1 0 101.414-1.414l-4.344-4.344a9.157 9.157 0 002.077-5.816c0-5.14-4.226-9.28-9.407-9.28zm-7.407 9.28c0-4.006 3.302-7.28 7.407-7.28s7.407 3.274 7.407 7.28-3.302 7.279-7.407 7.279-7.407-3.273-7.407-7.28z"/></svg></span>
            <input class="search-box" id="search-input" type="text" placeholder="Tìm bài hát, ca sĩ, album..." value="${initialQuery || ''}" autocomplete="off" />
            <div class="search-suggestions" id="search-suggestions"></div>
        </div>`;
        html += '<div id="search-results"></div>';
        html += '</div>';

        this.pageContent.innerHTML = html;

        const input = document.getElementById('search-input');
        const suggestions = document.getElementById('search-suggestions');

        input.focus();

        input.addEventListener('input', () => {
            clearTimeout(this._searchTimeout);
            const q = input.value.trim();
            if (q.length < 2) {
                suggestions.classList.remove('show');
                return;
            }
            this._searchTimeout = setTimeout(() => this._doSearch(q), 400);
        });

        input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                const q = input.value.trim();
                if (q) {
                    suggestions.classList.remove('show');
                    this._doSearch(q);
                }
            }
        });

        // Click outside to close suggestions
        document.addEventListener('click', (e) => {
            if (!e.target.closest('.search-wrapper')) {
                suggestions.classList.remove('show');
            }
        });

        if (initialQuery) {
            this._doSearch(initialQuery);
        }
    },

    async _doSearch(query) {
        const resultsEl = document.getElementById('search-results');
        if (!resultsEl) return;
        resultsEl.innerHTML = '<div class="loading-spinner"><div class="spinner"></div></div>';

        const json = await this.fetchApi(`/api/search?q=${encodeURIComponent(query)}`);

        if (!json || json.err !== 0) {
            resultsEl.innerHTML = '<p style="text-align:center;padding:40px;color:var(--text-muted)">Không tìm thấy kết quả</p>';
            return;
        }

        const data = json.data;
        let html = '';

        // Songs
        if (data.songs && data.songs.length > 0) {
            html += `<div class="section">
                <div class="section-header"><h2 class="section-title">Bài hát</h2></div>
                <div class="song-list stagger">`;
            data.songs.slice(0, 10).forEach((song, i) => {
                html += this._renderSongItem(song, i + 1, data.songs);
            });
            html += '</div></div>';
        }

        // Artists
        if (data.artists && data.artists.length > 0) {
            html += `<div class="section">
                <div class="section-header"><h2 class="section-title">Ca sĩ</h2></div>
                <div class="card-grid stagger">`;
            data.artists.slice(0, 6).forEach(artist => {
                html += `<div class="card artist-card" data-artist="${artist.alias || artist.name}" onclick="location.hash='/artist/${artist.alias || artist.name}'">
                    <div class="card-img">
                        <img src="${artist.thumbnailM || artist.thumbnail || '/music.png'}" alt="${artist.name}" loading="lazy" />
                    </div>
                    <div class="card-title">${artist.name}</div>
                    <div class="card-subtitle">${artist.totalFollow ? this._formatNumber(artist.totalFollow) + ' người theo dõi' : 'Ca sĩ'}</div>
                </div>`;
            });
            html += '</div></div>';
        }

        // Playlists
        if (data.playlists && data.playlists.length > 0) {
            html += `<div class="section">
                <div class="section-header"><h2 class="section-title">Playlist / Album</h2></div>
                <div class="card-grid stagger">`;
            data.playlists.slice(0, 6).forEach(item => {
                html += this._renderCard(item);
            });
            html += '</div></div>';
        }

        if (!html) {
            html = '<p style="text-align:center;padding:40px;color:var(--text-muted)">Không tìm thấy kết quả cho "' + query + '"</p>';
        }

        resultsEl.innerHTML = html;
        this._bindSongClicks();
        this._bindCardClicks();
    },

    // ═══════════════════════════════════
    // TRANG CA SĨ
    // ═══════════════════════════════════
    async loadArtist(alias) {
        if (!alias) return;
        this.showLoading();
        const json = await this.fetchApi(`/api/artist/${encodeURIComponent(alias)}`);

        if (!json || json.err !== 0) {
            this.pageContent.innerHTML = '<p style="text-align:center;padding:60px;color:var(--text-muted)">Không tìm thấy ca sĩ</p>';
            return;
        }

        const d = json.data;
        let html = '<div class="fade-in">';

        // Header
        html += `<div class="artist-header">
            <div class="artist-avatar">
                <img src="${d.thumbnailM || d.thumbnail || '/music.png'}" alt="${d.name}" />
            </div>
            <div class="artist-details">
                <div class="artist-type">Ca sĩ</div>
                <h1>${d.name}</h1>
                <div class="artist-followers">${d.totalFollow ? this._formatNumber(d.totalFollow) + ' người theo dõi' : ''}</div>
            </div>
        </div>`;

        // Bio
        if (d.biography) {
            const bio = d.biography.replace(/<[^>]*>/g, '').slice(0, 300);
            html += `<p class="artist-bio">${bio}...</p>`;
        }

        // Songs
        if (d.sections) {
            for (const section of d.sections) {
                if (section.sectionType === 'song' && section.items) {
                    html += `<div class="section">
                        <div class="section-header"><h2 class="section-title">${section.title || 'Bài hát nổi bật'}</h2></div>
                        <div class="song-list stagger">`;
                    section.items.slice(0, 15).forEach((song, i) => {
                        html += this._renderSongItem(song, i + 1, section.items);
                    });
                    html += '</div></div>';
                } else if (section.sectionType === 'playlist' && section.items) {
                    html += `<div class="section">
                        <div class="section-header"><h2 class="section-title">${section.title || 'Album / Single'}</h2></div>
                        <div class="card-grid stagger">`;
                    section.items.slice(0, 8).forEach(item => {
                        html += this._renderCard(item);
                    });
                    html += '</div></div>';
                }
            }
        }

        html += '</div>';
        this.pageContent.innerHTML = html;
        this._bindSongClicks();
        this._bindCardClicks();
    },

    // ═══════════════════════════════════
    // TRANG PLAYLIST / ALBUM
    // ═══════════════════════════════════
    async loadPlaylist(id) {
        if (!id) return;
        this.showLoading();
        const json = await this.fetchApi(`/api/playlist/${id}`);

        if (!json || json.err !== 0) {
            this.pageContent.innerHTML = '<p style="text-align:center;padding:60px;color:var(--text-muted)">Không thể tải playlist</p>';
            return;
        }

        const d = json.data;
        const songs = d.song?.items || [];
        let html = '<div class="fade-in">';

        // Back
        html += `<button class="btn-back" onclick="history.back()" style="background:transparent; border:none; color:var(--text-muted); cursor:pointer; display:inline-flex; align-items:center; gap:6px; padding:10px 0 20px 0; font-size:0.95rem; font-weight:600; text-transform:uppercase; letter-spacing:1px; transition:color 0.2s;" onmouseover="this.style.color='var(--accent)'" onmouseout="this.style.color='var(--text-muted)'">
            <svg viewBox="0 0 24 24" fill="currentColor" width="18" height="18"><path d="M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z"/></svg> Quay lại
        </button>`;

        // Header
        html += `<div class="album-header">
            <div class="album-cover">
                <img src="${d.thumbnailM || d.thumbnail || '/music.png'}" alt="${d.title}" />
            </div>
            <div class="album-details">
                <div class="album-type">${d.isAlbum ? 'Album' : 'Playlist'}</div>
                <h1>${d.title || ''}</h1>
                <div class="album-meta">
                    <span>${d.artistsNames || ''}</span>
                    ${d.releaseDate ? `<span class="dot"></span><span>${d.releaseDate}</span>` : ''}
                    <span class="dot"></span>
                    <span>${songs.length} bài hát</span>
                </div>
            </div>
        </div>`;

        // Actions
        html += `<div class="album-actions">
            <button class="btn-play-album" onclick="App._playAllSongs()">
                <svg viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg>
                Phát tất cả
            </button>
        </div>`;

        // Track list
        if (songs.length > 0) {
            html += '<div class="song-list stagger" id="album-songs">';
            songs.forEach((song, i) => {
                html += this._renderSongItem(song, i + 1, songs);
            });
            html += '</div>';
        }

        html += '</div>';
        this.pageContent.innerHTML = html;
        this._currentPlaylistSongs = songs;
        this._bindSongClicks();
    },

    _playAllSongs() {
        const songs = this._currentPlaylistSongs;
        if (songs && songs.length > 0) {
            Player.playSong(songs[0], songs, 0);
        }
    },

    // ═══════════════════════════════════
    // BÀI HÁT YÊU THÍCH (LIBRARY CŨ)
    // ═══════════════════════════════════
    loadLibrary() {
        const likedSongs = MusicLibrary.getLikedSongs();

        let html = '<div class="fade-in">';
        html += `
            <div class="section-header" style="flex-wrap: wrap; gap: 10px;">
                <h2 class="section-title" style="font-size:2rem">Bài hát yêu thích</h2>
                <!-- Thanh Search (lọc bài hát yêu thích) -->
                ${likedSongs.length > 0 ? `
                <div style="flex:1; min-width:200px; display:flex; justify-content:flex-end;">
                    <div class="search-box" style="margin: 0; max-width: 300px;">
                        <input type="text" id="filter-liked-input" placeholder="Tìm trong bài hát yêu thích..." oninput="App._filterLikedSongs()" style="padding: 10px 16px; border-radius: 20px; border: none; background: rgba(255,255,255,0.1); color: #fff; width: 100%; outline: none;" />
                    </div>
                </div>` : ''}
            </div>
        `;

        if (likedSongs.length === 0) {
            html += `<div class="library-empty">
                <svg viewBox="0 0 24 24"><path d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z"/></svg>
                <h2>Chưa có bài hát nào</h2>
                <p>Hãy bấm nút ❤️ trên thanh phát nhạc để thêm bài hát yêu thích vào đây!</p>
            </div>`;
        } else {
            // Liked Songs
            html += `<div class="section">
                <div class="section-header">
                    <h2 class="section-title" style="font-size:1rem; color:var(--text-muted)">Tổng ${likedSongs.length} bài</h2>
                    <button class="btn-play-album" onclick="App._playLikedSongs()" style="padding:8px 20px;font-size:0.82rem">
                        <svg viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg> Phát tất cả
                    </button>
                </div>
                <div class="song-list stagger" id="liked-songs-container">`;
            likedSongs.forEach((song, i) => {
                html += this._renderSongItem(song, i + 1, likedSongs);
            });
            html += '</div></div>';
        }

        html += '</div>';
        this.pageContent.innerHTML = html;
        this._bindSongClicks();
        this._bindCardClicks();
    },

    _filterLikedSongs() {
        const query = document.getElementById('filter-liked-input')?.value.toLowerCase().trim() || '';
        const container = document.getElementById('liked-songs-container');
        if (!container) return;

        const items = container.querySelectorAll('.song-item');
        let count = 0;
        items.forEach(item => {
            const nameEl = item.querySelector('.song-name');
            const artistEl = item.querySelector('.song-artist');
            const name = nameEl ? nameEl.textContent.toLowerCase() : '';
            const artist = artistEl ? artistEl.textContent.toLowerCase() : '';

            if (name.includes(query) || artist.includes(query)) {
                item.style.display = 'grid'; // because song-item uses grid
                count++;
                // update index visually
                const indexEl = item.querySelector('.song-index-num');
                if (indexEl) indexEl.textContent = count;
            } else {
                item.style.display = 'none';
            }
        });
    },

    _playLikedSongs() {
        const songs = MusicLibrary.getLikedSongs();
        if (songs.length > 0) {
            Player.playSong(songs[0], songs, 0);
        }
    },

    // ═══════════════════════════════════
    // PLAYLIST CỦA TÔI
    // ═══════════════════════════════════
    loadMyPlaylist() {
        if (location.hash.startsWith('#/myplaylist/') && location.hash.length > '#/myplaylist/'.length) {
            // Đây là xem chi tiết 1 playlist tự tạo, sẽ được catch ở hash nhưng phòng hờ
            const pid = location.hash.replace('#/myplaylist/', '');
            this.loadMyPlaylistDetail(pid);
            return;
        }

        const playlists = MusicLibrary.getCustomPlaylists();
        const savedAlbums = MusicLibrary.getSavedAlbums();

        let html = '<div class="fade-in">';
        html += `
            <div class="section-header" style="flex-wrap: wrap; gap: 10px; align-items: center;">
                <h2 class="section-title" style="font-size:2rem; margin:0;">Playlist & Album</h2>
                <button class="btn-play-album" onclick="App._createNewPlaylist()" style="padding:8px 20px;font-size:0.82rem; margin-left:auto;">
                    <svg viewBox="0 0 24 24" style="margin-right:6px;"><path d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z"/></svg> Tạo Playlist mới
                </button>
            </div>
        `;

        if (playlists.length === 0 && savedAlbums.length === 0) {
            html += `<div class="library-empty">
                <svg viewBox="0 0 24 24"><path d="M4 10h12v2H4zm0-4h12v2H4zm0 8h8v2H4zm10 0v6l5-3z"/></svg>
                <h2>Tạo playlist đầu tiên của bạn</h2>
                <p>Nhấp vào nút Tạo Playlist mới ở trên để bắt đầu.</p>
            </div>`;
        } else {
            // Custom Playlists
            if (playlists.length > 0) {
                html += `<div class="section" style="margin-top:20px;">
                    <div class="section-header"><h2 class="section-title">Playlist bạn tạo (${playlists.length})</h2></div>
                    <div class="card-grid stagger">`;
                playlists.forEach(pl => {
                    const thumb = pl.songs.length > 0 && pl.songs[0].thumbnailM ? pl.songs[0].thumbnailM : '/music.png';
                    html += `
                        <div class="card" onclick="location.hash='/myplaylist/${pl.id}'">
                            <div class="card-img" style="background:#333;">
                                <img src="${thumb}" alt="${pl.name}" loading="lazy" style="${pl.songs.length === 0 ? 'opacity:0.3; padding:30%;' : ''}" />
                                <div class="card-play-btn">
                                    <svg viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg>
                                </div>
                            </div>
                            <div class="card-title" title="${pl.name}">${pl.name}</div>
                            <div class="card-subtitle">${pl.songs.length} bài hát</div>
                        </div>
                    `;
                });
                html += '</div></div>';
            }

            // Saved Albums (Zingmp3 real ones)
            if (savedAlbums.length > 0) {
                html += `<div class="section">
                    <div class="section-header"><h2 class="section-title">Album đã theo dõi (${savedAlbums.length})</h2></div>
                    <div class="card-grid stagger">`;
                savedAlbums.forEach(album => {
                    html += this._renderCard(album);
                });
                html += '</div></div>';
            }
        }

        html += '</div>';
        this.pageContent.innerHTML = html;
        this._bindCardClicks();
    },

    _createNewPlaylist() {
        const name = prompt("Nhập tên Playlist mới:");
        if (name && name.trim()) {
            MusicLibrary.createPlaylist(name.trim());
            App.showToast('✅ Đã tạo playlist mới');
            this.loadMyPlaylist(); // Reload
        }
    },

    loadMyPlaylistDetail(pid) {
        // Find playlist
        const playlists = MusicLibrary.getCustomPlaylists();
        const pl = playlists.find(p => p.id === pid);
        if (!pl) {
            location.hash = '/myplaylist';
            return;
        }

        // Add dynamic current PID for _playPlaylist if needed
        this._currentCustomPlaylistSongs = pl.songs;

        const thumb = pl.songs.length > 0 && pl.songs[0].thumbnailM ? pl.songs[0].thumbnailM : '/music.png';
        const totalSongs = pl.songs.length;

        let html = '<div class="fade-in">';

        // Header
        html += `<div class="artist-header">
            <div class="artist-avatar" style="border-radius: 8px;">
                <img src="${thumb}" alt="${pl.name}" style="${pl.songs.length === 0 ? 'opacity:0.3; object-fit:contain; padding:20%;' : ''}" />
            </div>
            <div class="artist-details">
                <div class="artist-type" style="cursor:pointer;" onclick="location.hash='/myplaylist'">< Quay lại Playlist của tôi</div>
                <h1 style="margin-top:10px;">${pl.name}</h1>
                <div class="artist-followers" style="margin-bottom:15px;">Playlist tự tạo • ${totalSongs} bài hát</div>
                <div style="display:flex; gap:10px;">
                    <button class="btn-play-album" onclick="App._playCustomPlaylist()" ${totalSongs === 0 ? 'disabled style="opacity:0.5"' : ''}>
                        <svg viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg> PHÁT
                    </button>
                    <button class="btn-play-album" onclick="App._deleteCustomPlaylist('${pl.id}')" style="background: rgba(255,255,255,0.1); color: var(--text-primary);">
                        <svg viewBox="0 0 24 24" style="fill: currentColor; width: 18px; margin-right: 4px;"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zm2.46-7.12l1.41-1.41L12 12.59l2.12-2.12 1.41 1.41L13.41 14l2.12 2.12-1.41 1.41L12 15.41l-2.12 2.12-1.41-1.41L10.59 14l-2.13-2.12zM15.5 4l-1-1h-5l-1 1H5v2h14V4z"/></svg> Xóa
                    </button>
                </div>
            </div>
        </div>`;

        // Songs
        if (totalSongs === 0) {
            html += `<div style="text-align:center; margin-top:50px; color:var(--text-muted)">
                <svg viewBox="0 0 24 24" style="width:48px; fill:currentColor; margin-bottom:10px;"><path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/></svg>
                <h3>Playlist trống</h3>
                <p>Hãy tìm nhạc và nhấm vào ••• trên bài hát để thêm vào playlist này.</p>
            </div>`;
        } else {
            html += `<div class="section"><div class="song-list stagger">`;
            pl.songs.forEach((song, i) => {
                html += this._renderSongItem(song, i + 1, pl.songs);
            });
            html += `</div></div>`;
        }

        html += '</div>';
        this.pageContent.innerHTML = html;
        this._bindSongClicks();
    },

    _playCustomPlaylist() {
        if (this._currentCustomPlaylistSongs && this._currentCustomPlaylistSongs.length > 0) {
            Player.playSong(this._currentCustomPlaylistSongs[0], this._currentCustomPlaylistSongs, 0);
        }
    },

    _deleteCustomPlaylist(pid) {
        if (confirm("Bạn có chắc chắn muốn xóa playlist này?")) {
            MusicLibrary.deletePlaylist(pid);
            App.showToast("Đã xóa playlist");
            window.location.hash = '/myplaylist';
        }
    },
    /* ===========================
    KARAOKE PAGE
    =========================== */

    loadKaraoke() {

        this.pageContent.innerHTML = `
            <div class="karaoke-page">
                <h2>🎤 Karaoke Online</h2>

                <div class="karaoke-search-box">
                    <input id="karaoke-search"
                        placeholder="Tìm karaoke..."
                        class="karaoke-input"/>

                    <button id="voice-btn" class="voice-btn">
                        <i class="fa-solid fa-microphone"></i>
                    </button>
                </div>

                <div id="karaoke-result"></div>
            </div>
        `;

        this.initKaraokeSearch();
        this.initVoiceSearch(); // 👈 thêm
    },

    initVoiceSearch() {

        const SpeechRecognition =
            window.SpeechRecognition ||
            window.webkitSpeechRecognition;

        if (!SpeechRecognition) {
            alert("Trình duyệt không hỗ trợ tìm kiếm giọng nói");
            return;
        }

        const recognition = new SpeechRecognition();

        recognition.lang = "vi-VN";
        recognition.interimResults = false;
        recognition.maxAlternatives = 1;

        const btn = document.getElementById("voice-btn");
        const input = document.getElementById("karaoke-search");

        btn.onclick = () => {
            recognition.start();
            btn.classList.add("listening");
        };

        recognition.onresult = (event) => {

            const text = event.results[0][0].transcript;

            input.value = text;

            this.searchKaraoke(text);
        };

        recognition.onend = () => {
            btn.classList.remove("listening");
        };

        recognition.onerror = () => {
            btn.classList.remove("listening");
        };
    },

    initKaraokeSearch() {

        const input = document.getElementById("karaoke-search");

        input.addEventListener("keypress", e => {

            if (e.key === "Enter") {
                this.searchKaraoke(input.value);
            }

        });
    },

    async searchKaraoke(keyword) {

        if (!keyword) return;

        const resultDiv = document.getElementById("karaoke-result");
        resultDiv.innerHTML = "Đang tìm karaoke...";

        const query = encodeURIComponent(keyword + " karaoke");

        const url =
            `https://www.googleapis.com/youtube/v3/search?part=snippet&maxResults=8&type=video&q=${query}&key=${ytapi_key}`;

        try {
            const res = await fetch(url);
            const data = await res.json();

            if (!data.items) {
                resultDiv.innerHTML = "Không tìm thấy video";
                return;
            }

            this.renderKaraoke(data.items);
        }
        catch (err) {
            console.error(err);
            resultDiv.innerHTML = "Lỗi tải karaoke";
        }
    },

    renderKaraoke(videos) {

        const resultDiv = document.getElementById("karaoke-result");

        resultDiv.innerHTML = videos.map(v => {

            const id = v.id.videoId;
            const title = v.snippet.title;
            const thumb = v.snippet.thumbnails.medium.url;

            return `
                <div class="karaoke-item"
                    onclick="App.playKaraoke('${id}')">

                    <img src="${thumb}" />
                    <p>${title}</p>

                </div>
            `;
        }).join("");
    },

    playKaraoke(videoId) {

        this.pageContent.innerHTML = `
            <div class="karaoke-player">

                <div id="yt-player"></div>

                <div id="karaoke-score" class="karaoke-score hidden">
                    <h2>🎤 Bạn hát xong rồi!</h2>
                    <p id="score-text"></p>
                    <button onclick="App.loadKaraoke()">Hát tiếp</button>
                </div>

            </div>
        `;

        this.loadYoutubePlayer(videoId);
    },

    loadYoutubePlayer(videoId) {

        const self = this;

        window.player = new YT.Player('yt-player', {
            height: '600',
            width: '100%',
            videoId: videoId,

            events: {
                onReady: e => {
                    e.target.playVideo();
                },

                onStateChange: function (e) {

                    // 0 = video ended
                    if (e.data === 0) {
                        self.showScore();
                    }

                }
            }
        });
    },

    showScore() {

        const scoreBox = document.getElementById("karaoke-score");

        const score =
            Math.floor(Math.random() * 21) + 80; // 80-100 điểm

        document.getElementById("score-text")
            .innerHTML = `⭐ Điểm của bạn: <b>${score}</b>/100`;

        scoreBox.classList.remove("hidden");
    },

    // ═══════════════════════════════════
    // SHARED RENDER HELPERS
    // ═══════════════════════════════════

    _renderCard(item) {
        const id = item.encodeId || '';
        const title = item.title || '';
        const thumb = item.thumbnailM || item.thumbnail || '/music.png';
        const sub = item.artistsNames || item.sortDescription || '';

        return `<div class="card" data-playlist-id="${id}" onclick="location.hash='/playlist/${id}'">
            <div class="card-img">
                <img src="${thumb}" alt="${title}" loading="lazy" />
                <div class="card-play-btn">
                    <svg viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg>
                </div>
            </div>
            <div class="card-title" title="${title}">${title}</div>
            <div class="card-subtitle" title="${sub}">${sub}</div>
        </div>`;
    },

    _renderSongItem(song, index, songList) {
        const id = song.encodeId || '';
        const title = song.title || '';
        const artist = song.artistsNames || '';
        const thumb = song.thumbnailM || song.thumbnail || '/music.png';
        const duration = song.duration ? this._formatDuration(song.duration) : '';
        const albumTitle = song.album?.title || '';
        const liked = MusicLibrary.isSongLiked(id);
        const artistLink = song.artists?.[0]?.alias ? `onclick="event.stopPropagation();location.hash='/artist/${song.artists[0].alias}'"` : '';

        return `<div class="song-item" data-id="${id}" data-song='${JSON.stringify(song).replace(/'/g, "&#39;")}'>
            <div class="song-index">
                <span class="song-index-num">${index}</span>
                <span class="song-index-play"><svg viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg></span>
            </div>
            <div class="song-thumb"><img src="${thumb}" alt="" loading="lazy" /></div>
            <div class="song-info">
                <div class="song-name">${title}</div>
                <div class="song-artist" ${artistLink}>${artist}</div>
            </div>
            <div class="song-album">${albumTitle}</div>
            <div class="song-duration">${duration}</div>
            <button class="song-like-btn ${liked ? 'liked' : ''}" data-id="${id}" onclick="event.stopPropagation();App._toggleLikeSong(this, '${id}')">
                <svg viewBox="0 0 24 24"><path d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z"/></svg>
            </button>
            <button class="song-more-btn" onclick="event.stopPropagation();App._showSongMenu(event, this)">
                <svg viewBox="0 0 24 24"><path d="M6 10c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm12 0c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm-6 0c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z"/></svg>
            </button>
        </div>`;
    },

    _renderChartItem(song, rank) {
        const id = song.encodeId || '';
        const title = song.title || '';
        const artist = song.artistsNames || '';
        const thumb = song.thumbnailM || song.thumbnail || '/music.png';
        const duration = song.duration ? this._formatDuration(song.duration) : '';
        const liked = MusicLibrary.isSongLiked(id);
        const rankClass = rank <= 3 ? `top-${rank}` : '';

        return `<div class="chart-item song-item" data-id="${id}" data-song='${JSON.stringify(song).replace(/'/g, "&#39;")}'>
            <div class="chart-rank ${rankClass}">${rank}</div>
            <div class="song-thumb"><img src="${thumb}" alt="" loading="lazy" /></div>
            <div class="song-info">
                <div class="song-name">${title}</div>
                <div class="song-artist">${artist}</div>
            </div>
            <div class="song-duration">${duration}</div>
            <button class="song-like-btn ${liked ? 'liked' : ''}" data-id="${id}" onclick="event.stopPropagation();App._toggleLikeSong(this, '${id}')">
                <svg viewBox="0 0 24 24"><path d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z"/></svg>
            </button>
            <button class="song-more-btn" onclick="event.stopPropagation();App._showSongMenu(event, this)">
                <svg viewBox="0 0 24 24"><path d="M6 10c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm12 0c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm-6 0c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z"/></svg>
            </button>
        </div>`;
    },

    _bindSongClicks() {
        document.querySelectorAll('.song-item').forEach(el => {
            el.addEventListener('click', (e) => {
                if (e.target.closest('.song-like-btn') || e.target.closest('.song-artist')) return;
                try {
                    const songData = JSON.parse(el.dataset.song);
                    // Find queue from parent list
                    const parent = el.closest('.song-list');
                    let queue = [];
                    if (parent) {
                        parent.querySelectorAll('.song-item').forEach(item => {
                            try { queue.push(JSON.parse(item.dataset.song)); } catch { }
                        });
                    }
                    const idx = queue.findIndex(s => s.encodeId === songData.encodeId);
                    Player.playSong(songData, queue.length > 0 ? queue : [songData], idx >= 0 ? idx : 0);
                } catch (err) {
                    console.error('Failed to parse song data:', err);
                }
            });
        });
    },

    _bindCardClicks() {
        // Already handled via onclick in HTML
    },

    _toggleLikeSong(btn, id) {
        // Find song data from the closest song-item
        const songEl = btn.closest('.song-item');
        if (!songEl) return;
        try {
            const song = JSON.parse(songEl.dataset.song);
            const liked = MusicLibrary.toggleSong(song);
            btn.classList.toggle('liked', liked);
            // Sync player like button
            if (Player.currentSong && Player.currentSong.encodeId === id) {
                document.getElementById('player-like-btn').classList.toggle('liked', liked);
            }
            App.showToast(liked ? 'Đã thêm vào Yêu thích ❤️' : 'Đã xóa khỏi Yêu thích');
        } catch { }
    },

    _showSongMenu(e, btn) {
        // Prevent default and stop propagation
        e.preventDefault();
        e.stopPropagation();

        // Xóa menu cũ
        const oldMenu = document.getElementById('custom-song-menu');
        if (oldMenu) oldMenu.remove();

        // Lấy thông tin bài hát
        const songEl = btn.closest('.song-item');
        if (!songEl) return;
        const songData = JSON.parse(songEl.dataset.song);
        const encodeId = songData.encodeId;

        // Render playlist submenu items
        const playlists = MusicLibrary.getCustomPlaylists();
        const playlistItemsHtml = playlists.length === 0
            ? '<div style="padding: 10px 14px; color:var(--text-muted)">Chưa có playlist nào. Hãy tạo một playlist mới.</div>'
            : playlists.map(p => `
                <div class="cm-item add-to-playlist-action" data-pid="${p.id}">
                    ${p.name}
                </div>
            `).join('');

        // Kiểm tra xem bài hát này hiện tại đang nằm trong một playlist không?
        const currentPlaylistMatch = window.location.hash.match(/#\/myplaylist\/([^/]+)/);
        const currentPid = currentPlaylistMatch ? currentPlaylistMatch[1] : null;

        const removeBtnHtml = currentPid
            ? `<div class="cm-divider"></div>
               <div class="cm-item remove-from-playlist-action" data-pid="${currentPid}">
                   <svg viewBox="0 0 24 24"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zm2.46-7.12l1.41-1.41L12 12.59l2.12-2.12 1.41 1.41L13.41 14l2.12 2.12-1.41 1.41L12 15.41l-2.12 2.12-1.41-1.41L10.59 14l-2.13-2.12zM15.5 4l-1-1h-5l-1 1H5v2h14V4z"/></svg> 
                   Xóa khỏi playlist này
               </div>`
            : '';

        // Tạo phần tử menu
        const menu = document.createElement('div');
        menu.id = 'custom-song-menu';
        menu.className = 'custom-context-menu';

        // Lấy tọa độ click
        const rect = btn.getBoundingClientRect();
        const top = rect.bottom + window.scrollY;
        const left = rect.right - 220 + window.scrollX; // Canh phải

        menu.style.top = top + 'px';
        menu.style.left = left + 'px';

        menu.innerHTML = `
            <div class="cm-submenu">
                <div class="cm-item">
                    <svg viewBox="0 0 24 24"><path d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z"/></svg> Add to playlist
                    <svg viewBox="0 0 24 24" style="margin-left:auto; width:16px;height:16px"><path d="M8.59 16.59L13.17 12 8.59 7.41 10 6l6 6-6 6-1.41-1.41z"/></svg>
                </div>
                <div class="cm-submenu-content">
                    ${playlistItemsHtml}
                </div>
            </div>
            ${removeBtnHtml}
        `;

        document.body.appendChild(menu);

        // Events cho menu vừa tạo
        menu.querySelectorAll('.add-to-playlist-action').forEach(item => {
            item.addEventListener('click', () => {
                const pid = item.dataset.pid;
                const result = MusicLibrary.addSongToPlaylist(pid, songData);
                if (result) App.showToast('✅ Đã thêm bài hát vào playlist');
                else App.showToast('Bài hát đã tồn tại trong playlist này');
                menu.remove();
            });
        });

        const removeBtn = menu.querySelector('.remove-from-playlist-action');
        if (removeBtn) {
            removeBtn.addEventListener('click', () => {
                const pid = removeBtn.dataset.pid;
                MusicLibrary.removeSongFromPlaylist(pid, encodeId);
                App.showToast('🗑️ Đã xóa khỏi playlist');
                menu.remove();
                if (App.currentPage === 'myplaylist') {
                    App.loadMyPlaylistDetail(pid); // reload nếu đang ở trong trang chi tiết playlist
                }
            });
        }

        // Tự đóng menu khi click ra ngoài
        setTimeout(() => {
            const closeMenu = (evt) => {
                if (!menu.contains(evt.target)) {
                    menu.remove();
                    document.removeEventListener('click', closeMenu);
                }
            };
            document.addEventListener('click', closeMenu);
        }, 0);
    },

    // ── Utility ──
    _formatDuration(seconds) {
        const m = Math.floor(seconds / 60);
        const s = Math.floor(seconds % 60);
        return `${m}:${s.toString().padStart(2, '0')}`;
    },

    _formatNumber(n) {
        if (n >= 1000000) return (n / 1000000).toFixed(1) + 'M';
        if (n >= 1000) return (n / 1000).toFixed(0) + 'K';
        return n.toString();
    },

};


// ── Start App ──
document.addEventListener('DOMContentLoaded', () => App.init());



const queuePanel = document.getElementById("queue-panel");
const btnQueue = document.getElementById("btn-queue");
const btnMobileQueue = document.getElementById("btn-mobile-queue");
const closeQueue = document.getElementById("close-queue");

if (btnQueue) {
    btnQueue.onclick = () => {
        if (window.player) window.player.renderQueue();
        queuePanel.classList.toggle("show");
    };
}

if (btnMobileQueue) {
    btnMobileQueue.onclick = () => {
        if (window.player) window.player.renderQueue();
        queuePanel.classList.toggle("show");
    };
}

closeQueue.onclick = () => {
    queuePanel.classList.remove("show");
};