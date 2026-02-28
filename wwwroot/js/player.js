/* ============================================================
   player.js - Trình phát nhạc HTML5
   ============================================================ */

const Player = {
    audio: null,
    queue: [],
    currentIndex: -1,
    isPlaying: false,
    shuffle: false,
    repeat: 0, // 0=off, 1=all, 2=one
    volume: 0.7,
    currentSong: null,
    currentLyrics: [],
    _activeLyricIndex: -1,
    _progressDragging: false,
    _volumeDragging: false,

    init() {
        this.audio = document.getElementById('audio-player');
        this.els = {
            playBtn: document.getElementById('btn-play'),
            playIcon: document.getElementById('play-icon'),
            prevBtn: document.getElementById('btn-prev'),
            nextBtn: document.getElementById('btn-next'),
            shuffleBtn: document.getElementById('btn-shuffle'),
            repeatBtn: document.getElementById('btn-repeat'),
            thumb: document.getElementById('player-thumb'),
            thumbImg: document.getElementById('player-thumb-img'),
            songName: document.getElementById('player-song-name'),
            songArtist: document.getElementById('player-song-artist'),
            likeBtn: document.getElementById('player-like-btn'),
            progressBar: document.getElementById('progress-bar'),
            progressFill: document.getElementById('progress-fill'),
            currentTime: document.getElementById('progress-current'),
            duration: document.getElementById('progress-duration'),
            volumeBar: document.getElementById('volume-bar'),
            volumeFill: document.getElementById('volume-fill'),
            volumeBtn: document.getElementById('volume-btn'),
            btnLyrics: document.getElementById('btn-lyrics'),
            lyricsModal: document.getElementById('lyrics-modal'),
            btnCloseLyrics: document.getElementById('btn-close-lyrics'),
            lyricsOverlay: document.getElementById('lyrics-close'),
            lyricsImg: document.getElementById('lyrics-img'),
            lyricsTitle: document.getElementById('lyrics-title'),
            lyricsArtist: document.getElementById('lyrics-artist'),
            lyricsText: document.getElementById('lyrics-text'),

            btnQueue: document.getElementById('btn-queue'),
            queuePanel: document.getElementById('queue-panel'),
            queueClose: document.getElementById('queue-close'),
            btnCloseQueue: document.getElementById('btn-close-queue'),
            queueList: document.getElementById('queue-list')
        };

        this.audio.volume = this.volume;
        this._bindEvents();
    },

    _bindEvents() {
        // Play/Pause
        this.els.playBtn.addEventListener('click', () => this.togglePlay());

        // Prev/Next
        this.els.prevBtn.addEventListener('click', () => this.prev());
        this.els.nextBtn.addEventListener('click', () => this.next());

        // Shuffle
        this.els.shuffleBtn.addEventListener('click', () => {
            this.shuffle = !this.shuffle;
            this.els.shuffleBtn.classList.toggle('active', this.shuffle);
            App.showToast(this.shuffle ? 'Đã bật trộn bài' : 'Đã tắt trộn bài');
        });

        // Repeat
        this.els.repeatBtn.addEventListener('click', () => {
            this.repeat = (this.repeat + 1) % 3;
            this.els.repeatBtn.classList.toggle('active', this.repeat > 0);
            const labels = ['Tắt lặp lại', 'Lặp lại tất cả', 'Lặp lại 1 bài'];
            App.showToast(labels[this.repeat]);
            if (this.repeat === 2) {
                this.els.repeatBtn.innerHTML = '<svg viewBox="0 0 24 24"><path d="M7 7h10v3l4-4-4-4v3H5v6h2V7zm10 10H7v-3l-4 4 4 4v-3h12v-6h-2v4z"/><text x="12" y="16" text-anchor="middle" font-size="7" fill="currentColor" font-weight="bold">1</text></svg>';
            } else {
                this.els.repeatBtn.innerHTML = '<svg viewBox="0 0 24 24"><path d="M7 7h10v3l4-4-4-4v3H5v6h2V7zm10 10H7v-3l-4 4 4 4v-3h12v-6h-2v4z"/></svg>';
            }
        });

        // Like
        this.els.likeBtn.addEventListener('click', () => {
            if (!this.currentSong) return;
            const liked = MusicLibrary.toggleSong(this.currentSong);
            this.els.likeBtn.classList.toggle('liked', liked);
            App.showToast(liked ? 'Đã thêm vào Yêu thích ❤️' : 'Đã xóa khỏi Yêu thích');
            // Update song list UI
            document.querySelectorAll(`.song-like-btn[data-id="${this.currentSong.encodeId}"]`).forEach(el => {
                el.classList.toggle('liked', liked);
            });
        });

        // Audio events
        this.audio.addEventListener('timeupdate', () => this._onTimeUpdate());
        this.audio.addEventListener('loadedmetadata', () => this._onLoaded());
        this.audio.addEventListener('ended', () => this._onEnded());
        this.audio.addEventListener('play', () => this._setPlayingUI(true));
        this.audio.addEventListener('pause', () => this._setPlayingUI(false));
        this.audio.addEventListener('error', (e) => {
            console.error('Audio error:', e);
            App.showToast('Không thể phát bài hát này. Đang thử bài tiếp...');
            setTimeout(() => this.next(), 1500);
        });

        // Progress bar click/drag
        this.els.progressBar.addEventListener('mousedown', (e) => {
            this._progressDragging = true;
            this._seekTo(e);
        });
        document.addEventListener('mousemove', (e) => {
            if (this._progressDragging) this._seekTo(e);
        });
        document.addEventListener('mouseup', () => {
            this._progressDragging = false;
        });

        // Volume bar click/drag
        this.els.volumeBar.addEventListener('mousedown', (e) => {
            this._volumeDragging = true;
            this._setVolume(e);
        });
        document.addEventListener('mousemove', (e) => {
            if (this._volumeDragging) this._setVolume(e);
        });
        document.addEventListener('mouseup', () => {
            this._volumeDragging = false;
        });

        // Volume toggle mute
        this.els.volumeBtn.addEventListener('click', () => {
            if (this.audio.volume > 0) {
                this._prevVolume = this.audio.volume;
                this.audio.volume = 0;
                this.els.volumeFill.style.width = '0%';
            } else {
                this.audio.volume = this._prevVolume || 0.7;
                this.els.volumeFill.style.width = (this.audio.volume * 100) + '%';
            }
        });

        // Keyboard controls
        document.addEventListener('keydown', (e) => {
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
            if (e.code === 'Space') {
                e.preventDefault();
                this.togglePlay();
            }
            if (e.code === 'ArrowRight' && e.ctrlKey) this.next();
            if (e.code === 'ArrowLeft' && e.ctrlKey) this.prev();
        });

        // Lyrics Modal
        this.els.btnLyrics.addEventListener('click', () => {
            this.showLyricsModal();
        });

        this.els.btnCloseLyrics.addEventListener('click', () => {
            this.els.lyricsModal.classList.remove('show');
        });

        this.els.lyricsOverlay.addEventListener('click', () => {
            this.els.lyricsModal.classList.remove('show');
        });

        // Click tên bài hát → mở lyrics
        this.els.songName.addEventListener('click', () => {
            if (!this.currentSong) return;
            this.showLyricsModal();
        });

        // Click tên ca sĩ → tới trang artist
        this.els.songArtist.addEventListener('click', () => {
            if (!this.currentSong || !this.currentSong.artists || !this.currentSong.artists.length) return;
            const alias = this.currentSong.artists[0].alias || this.currentSong.artists[0].link || '';
            if (alias) {
                App.navigate('artist', { alias: alias.replace('/nghe-si/', '').replace('/', '') });
            }
        });

        //Queue
        if (this.els.btnQueue) {
            this.els.btnQueue.addEventListener('click', () => {
                this.renderQueue();
                this.els.queuePanel?.classList.add('show');
            });
        }

        if (this.els.queueClose) {
            this.els.queueClose.addEventListener('click', () =>
                this.els.queuePanel?.classList.remove('show')
            );
        }

        if (this.els.btnCloseQueue) {
            this.els.btnCloseQueue.addEventListener('click', () =>
                this.els.queuePanel?.classList.remove('show')
            );
        }
    },

    // ── Play a song ──
    async playSong(song, queue = null, index = -1) {
        if (!song || !song.encodeId) return;
        this.currentSong = song;
        this.currentLyrics = [];
        this._activeLyricIndex = -1;

        // Cập nhật lại lyrics nếu modal đang mở
        if (this.els.lyricsModal.classList.contains('show')) {
            this.showLyricsModal();
        }

        if (queue) {
            this.queue = queue;
            this.currentIndex = index >= 0 ? index : queue.findIndex(s => s.encodeId === song.encodeId);
        }

        // Update UI immediately
        this.els.thumbImg.src = song.thumbnailM || song.thumbnail || '/music.png';
        this.els.songName.textContent = song.title || 'Không rõ';
        this.els.songArtist.textContent = song.artistsNames || 'Không rõ';

        // Highlight artist nếu có trang
        if (song.artists && song.artists.length) {
            this.els.songArtist.style.textDecoration = 'underline dotted';
            this.els.songArtist.title = 'Xem trang ' + (song.artists[0].name || '');
        } else {
            this.els.songArtist.style.textDecoration = 'none';
        }
        this.els.likeBtn.classList.toggle('liked', MusicLibrary.isSongLiked(song.encodeId));
        this.els.thumb.classList.remove('spinning');

        // Mark playing in song list  
        document.querySelectorAll('.song-item.playing').forEach(el => el.classList.remove('playing'));
        document.querySelectorAll(`.song-item[data-id="${song.encodeId}"]`).forEach(el => el.classList.add('playing'));

        // Fetch streaming URL
        try {
            const res = await fetch(`/api/song/${song.encodeId}`);
            const json = await res.json();

            if (json.err === 0 && json.data) {
                const streamUrl = json.data['128'] || json.data['320'] || json.data.default;
                if (streamUrl) {
                    this.audio.src = streamUrl;
                    this.audio.play();
                    this.els.thumb.classList.add('spinning');
                    document.title = `${song.title} - ${song.artistsNames} | Music2.0`;
                } else {
                    App.showToast('Bài hát này yêu cầu VIP. Đang chuyển bài...');
                    setTimeout(() => this.next(), 1500);
                }
            } else {
                App.showToast('Không thể lấy link nhạc');
                setTimeout(() => this.next(), 1500);
            }
        } catch (err) {
            console.error('Failed to get song:', err);
            App.showToast('Lỗi kết nối');
        }
    },

    togglePlay() {
        if (!this.currentSong) return;
        if (this.audio.paused) {
            this.audio.play();
        } else {
            this.audio.pause();
        }
    },

    next() {
        if (this.queue.length === 0) return;
        let idx;
        if (this.shuffle) {
            idx = Math.floor(Math.random() * this.queue.length);
        } else {
            idx = (this.currentIndex + 1) % this.queue.length;
        }
        this.currentIndex = idx;
        this.playSong(this.queue[idx], null, idx);
    },

    prev() {
        if (this.queue.length === 0) return;
        // If > 3 seconds, restart. Otherwise previous.
        if (this.audio.currentTime > 3) {
            this.audio.currentTime = 0;
            return;
        }
        let idx;
        if (this.shuffle) {
            idx = Math.floor(Math.random() * this.queue.length);
        } else {
            idx = (this.currentIndex - 1 + this.queue.length) % this.queue.length;
        }
        this.currentIndex = idx;
        this.playSong(this.queue[idx], null, idx);
    },

    // ── Private ──
    _onTimeUpdate() {
        if (this._progressDragging) return;
        const { currentTime, duration } = this.audio;
        if (duration) {
            const pct = (currentTime / duration) * 100;
            this.els.progressFill.style.width = pct + '%';
            this.els.currentTime.textContent = this._formatTime(currentTime);
        }

        // Đồng bộ lời bài hát (karaoke 🎤)
        this._syncLyrics(currentTime);
    },

    _syncLyrics(currentTime) {
        if (!this.els.lyricsModal.classList.contains('show') || !this.currentLyrics || this.currentLyrics.length === 0) return;

        const timeMs = currentTime * 1000;
        let activeIndex = -1;

        // Tìm câu đang hát
        for (let i = 0; i < this.currentLyrics.length; i++) {
            if (timeMs >= this.currentLyrics[i].startTime) {
                activeIndex = i;
            } else {
                break; // Lời hát đã được sắp xếp tăng dần, nên chỉ cần break
            }
        }

        if (activeIndex !== -1 && this._activeLyricIndex !== activeIndex) {
            // Xoá active cũ
            const oldActive = document.querySelector('.lyrics-right p.active');
            if (oldActive) oldActive.classList.remove('active');

            // Set active mới
            const newActive = document.getElementById(`lyric-line-${activeIndex}`);
            if (newActive) {
                newActive.classList.add('active');
                this._activeLyricIndex = activeIndex;

                // Cuộn tự động
                const container = this.els.lyricsText;
                const offsetTop = newActive.offsetTop;
                const scrollPos = offsetTop - (container.clientHeight / 2) + (newActive.clientHeight / 2);

                container.scrollTo({
                    top: Math.max(0, scrollPos),
                    behavior: 'smooth'
                });
            }
        }
    },

    _onLoaded() {
        this.els.duration.textContent = this._formatTime(this.audio.duration);
    },

    _onEnded() {
        this.els.thumb.classList.remove('spinning');
        if (this.repeat === 2) {
            // Repeat one
            this.audio.currentTime = 0;
            this.audio.play();
            this.els.thumb.classList.add('spinning');
        } else if (this.repeat === 1 || this.currentIndex < this.queue.length - 1) {
            this.next();
        }
    },

    _setPlayingUI(playing) {
        this.isPlaying = playing;
        if (playing) {
            this.els.playIcon.innerHTML = '<path d="M6 19h4V5H6v14zm8-14v14h4V5h-4z"/>';
            this.els.thumb.classList.add('spinning');
        } else {
            this.els.playIcon.innerHTML = '<path d="M8 5v14l11-7z"/>';
            this.els.thumb.classList.remove('spinning');
        }
    },

    _seekTo(e) {
        const rect = this.els.progressBar.getBoundingClientRect();
        let pct = (e.clientX - rect.left) / rect.width;
        pct = Math.max(0, Math.min(1, pct));
        this.els.progressFill.style.width = (pct * 100) + '%';
        if (this.audio.duration) {
            this.audio.currentTime = pct * this.audio.duration;
        }
    },

    _setVolume(e) {
        const rect = this.els.volumeBar.getBoundingClientRect();
        let pct = (e.clientX - rect.left) / rect.width;
        pct = Math.max(0, Math.min(1, pct));
        this.audio.volume = pct;
        this.els.volumeFill.style.width = (pct * 100) + '%';
    },

    _formatTime(s) {
        if (!s || isNaN(s)) return '0:00';
        const m = Math.floor(s / 60);
        const sec = Math.floor(s % 60);
        return `${m}:${sec.toString().padStart(2, '0')}`;
    },

    // ── Lyrics ──
    async showLyricsModal() {
        if (!this.currentSong) {
            App.showToast('Chưa phát bài hát nào!');
            return;
        }

        this.els.lyricsModal.classList.add('show');
        this.els.lyricsImg.src = this.currentSong.thumbnailM || this.currentSong.thumbnail || '/music.png';
        this.els.lyricsTitle.textContent = this.currentSong.title || 'Không rõ';
        this.els.lyricsArtist.textContent = this.currentSong.artistsNames || 'Không rõ';
        this.els.lyricsText.innerHTML = '<div class="loading-spinner"><div class="spinner"></div></div>';

        try {
            const res = await fetch(`/api/lyrics/${this.currentSong.encodeId}`);
            const json = await res.json();

            if (json.err === 0 && json.data && json.data.sentences) {
                let html = '';
                this.currentLyrics = [];

                json.data.sentences.forEach((s, index) => {
                    const line = s.words.map(w => w.data).join(' ');
                    const startTime = s.words[0].startTime;

                    html += `<p id="lyric-line-${index}" onclick="Player.seekToTime(${startTime})">${line}</p>`;
                    this.currentLyrics.push({
                        startTime: startTime,
                        index: index
                    });
                });

                this.els.lyricsText.innerHTML = html || '<div class="lyrics-placeholder">Không có lời bài hát</div>';
                this._activeLyricIndex = -1; // Reset active
            } else {
                this.els.lyricsText.innerHTML = '<div class="lyrics-placeholder">Không có lời bài hát</div>';
            }
        } catch (err) {
            console.error('Failed to get lyrics:', err);
            this.els.lyricsText.innerHTML = '<div class="lyrics-placeholder">Lỗi khi tải lời bài hát</div>';
        }
    },

    seekToTime(timeMs) {
        if (this.audio && this.audio.duration) {
            this.audio.currentTime = timeMs / 1000;
            this.audio.play();
        }
    },

        //Queue
        renderQueue() {

        if (!this.queue.length) {
            this.els.queueList.innerHTML =
                '<div class="queue-empty">Chưa có bài hát</div>';
            return;
        }

        let html = '';

        this.queue.forEach((song, index) => {

            const playing = index === this.currentIndex
                ? 'playing'
                : '';

            html += `
            <div class="queue-item ${playing}"
                data-index="${index}">
                <img src="${song.thumbnailM || song.thumbnail || '/music.png'}">
                <div>
                    <div>${song.title}</div>
                    <small>${song.artistsNames || ''}</small>
                </div>
            </div>`;
        });

        this.els.queueList.innerHTML = html;

        // click play
        this.els.queueList.querySelectorAll('.queue-item')
            .forEach(el => {
                el.onclick = () => {
                    const i = +el.dataset.index;
                    this.currentIndex = i;
                    this.playSong(this.queue[i], null, i);
                    this.renderQueue();
                };
            });

        this._scrollQueueToActive();
    },

        _scrollQueueToActive(){
        const active =
            this.els.queueList.querySelector('.queue-item.playing');

        if(!active) return;

        active.scrollIntoView({
            block:'center',
            behavior:'smooth'
        });
    }
};
