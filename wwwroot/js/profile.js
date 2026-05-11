document.addEventListener('DOMContentLoaded', function () {
    const editUsernameBtn = document.getElementById('editUsernameBtn');
    const changeAvatarBtn = document.getElementById('changeAvatarBtn');
    const usernameModal = document.getElementById('usernameModal');
    const avatarModal = document.getElementById('avatarModal');
    const displayUsername = document.getElementById('displayUsername');
    const newUsernameInput = document.getElementById('newUsernameInput');

    const avatarInput = document.getElementById('avatarInput');
    const previewImg = document.getElementById('previewImg');
    const avatarPreview = document.getElementById('avatarPreview');
    const uploadText = document.getElementById('uploadText');
    const confirmUploadBtn = document.getElementById('confirmUploadBtn');

    if (editUsernameBtn && usernameModal) {
        editUsernameBtn.onclick = () => {
            if (newUsernameInput && displayUsername) {
                newUsernameInput.value = displayUsername.textContent.trim();
            }
            usernameModal.style.display = 'flex';
        };
    }

    if (changeAvatarBtn && avatarModal) {
        changeAvatarBtn.onclick = () => {
            avatarModal.style.display = 'flex';
        };
    }

    if (avatarInput) {
        avatarInput.onchange = function () {
            const file = this.files[0];
            if (!file) {
                return;
            }

            const fileName = file.name.length > 25 ? file.name.substring(0, 22) + "..." : file.name;
            if (uploadText) {
                uploadText.textContent = `Selected: ${fileName}`;
            }

            const reader = new FileReader();
            reader.onload = e => {
                if (previewImg && avatarPreview) {
                    previewImg.src = e.target.result;
                    avatarPreview.style.display = 'block';
                }
            };
            reader.readAsDataURL(file);

            const errorMessage = document.getElementById('uploadErrorMessage');
            if (errorMessage) {
                errorMessage.style.display = 'none';
            }
        };
    }

    if (confirmUploadBtn) {
        confirmUploadBtn.onclick = function () {
            const fileInput = document.getElementById('avatarInput');

            if (fileInput.files.length > 0) {
                confirmUploadBtn.textContent = 'UPLOADING...';
                confirmUploadBtn.disabled = true;

                setTimeout(() => {
                    showToast('Profile picture updated successfully!');
                    closeModal('avatarModal');
                    confirmUploadBtn.textContent = 'UPLOAD';
                    confirmUploadBtn.disabled = false;
                }, 1500);
            } else {
                showToast('Please select an image first.', true);

                const uploadArea = document.querySelector('.upload-area');
                if (uploadArea) {
                    uploadArea.style.borderColor = '2px solid #000';
                    setTimeout(() => {
                        uploadArea.style.borderColor = '2px solid #000';
                    }, 2000);
                }
            }
        };
    }

    window.onclick = event => {
        if (event.target === usernameModal) closeModal('usernameModal');
        if (event.target === avatarModal) closeModal('avatarModal');
    };
});

function closeModal(id) {
    const modal = document.getElementById(id);
    if (!modal) {
        return;
    }

    modal.style.display = 'none';

    if (id === 'avatarModal') {
        const avatarPreview = document.getElementById('avatarPreview');
        const uploadText = document.getElementById('uploadText');
        const avatarInput = document.getElementById('avatarInput');
        const errorMessage = document.getElementById('uploadErrorMessage');

        if (avatarPreview) avatarPreview.style.display = 'none';
        if (uploadText) uploadText.textContent = 'Click to upload or drag and drop';
        if (avatarInput) avatarInput.value = '';
        if (errorMessage) errorMessage.style.display = 'none';
    }
}

function saveUsername() {
    const input = document.getElementById('newUsernameInput');
    const usernameDisplay = document.getElementById('displayUsername');

    if (input && input.value.trim() === '') {
        showToast('ERROR: USERNAME CANNOT BE EMPTY', true);
        input.style.borderColor = '#000';
        return;
    }

    if (input && usernameDisplay) {
        usernameDisplay.textContent = input.value.toUpperCase();
        showToast('USERNAME UPDATED');
        closeModal('usernameModal');
        input.value = '';
        input.style.borderColor = '#eee';
    }
}

function showToast(message, isError = false) {
    let toast = document.querySelector('.toast-container');
    if (!toast) {
        toast = document.createElement('div');
        toast.className = 'toast-container';
        document.body.appendChild(toast);
    }

    toast.textContent = message;
    if (isError) {
        toast.classList.add('error');
    } else {
        toast.classList.remove('error');
    }

    setTimeout(() => toast.classList.add('show'), 10);

    setTimeout(() => {
        toast.classList.remove('show');
    }, 3000);
}
