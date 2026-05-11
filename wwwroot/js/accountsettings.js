// ==========================================
// 1. GLOBAL FUNCTIONS (Can be clicked by HTML)
// ==========================================

function openTab(evt, tabName) {
    document.querySelectorAll(".tab-content").forEach(c => c.classList.remove("active"));
    document.querySelectorAll(".tab").forEach(t => t.classList.remove("active"));

    const target = document.getElementById(tabName);
    if(target) target.classList.add("active");
    if(evt && evt.currentTarget) evt.currentTarget.classList.add("active");
}

function getUserId() {
    const idInput = document.getElementById("loggedInUserId");
    return idInput ? idInput.value : 0;
}

function closeAllModals() {
    document.querySelectorAll(".modal-overlay").forEach(modal => modal.style.display = "none");
}

function openUpdateModal(message = "Are you sure you want to save these changes?") {
    const messageElem = document.getElementById("updateModalMessage");
    if(messageElem) messageElem.innerText = message;
    closeAllModals();
    const modal = document.getElementById("updateModal");
    if(modal) modal.style.display = "flex";
}

async function confirmUpdate() {
    closeAllModals();
    
    // Send data to C# API
    const success = await saveBusinessProfile();
    
    // Show success popup if API says OK
    if(success) {
        await loadDashboardData();
        const successModal = document.getElementById("passwordChangedModal"); 
        if(successModal) successModal.style.display = "flex";
    }
}

// ------------------------------------------
// EMAIL CHANGE SECURE WORKFLOW
// ------------------------------------------

function openEmailChangeModal() {
    closeAllModals(); 
    
    // Clear out old text
    const newEmailInput = document.getElementById("newEmailInput");
    const passwordInput = document.getElementById("emailChangePassword");
    const otpInput = document.getElementById("emailOTPInput");
    
    if (newEmailInput) newEmailInput.value = "";
    if (passwordInput) passwordInput.value = "";
    if (otpInput) otpInput.value = "";
    
    // Reset view to Step 1
    document.getElementById("emailStep1").style.display = "block";
    document.getElementById("emailStep2").style.display = "none";
    
    // Open Modal
    const modal = document.getElementById("emailChangeModal");
    if(modal) modal.style.display = "flex";
}

async function requestEmailOTP() {
    const newEmail = document.getElementById("newEmailInput")?.value;
    const password = document.getElementById("emailChangePassword")?.value;

    if (!newEmail || !password) {
        alert("Please enter both your new email and current password.");
        return;
    }

    // TODO: Add C# API fetch call here later
    console.log("Simulating sending OTP to:", newEmail);

    // Switch UI to Step 2 (Verification)
    document.getElementById("pendingEmailSpan").innerText = newEmail;
    document.getElementById("emailStep1").style.display = "none";
    document.getElementById("emailStep2").style.display = "block";
}

async function verifyEmailChange() {
    const otp = document.getElementById("emailOTPInput")?.value;

    if (!otp || otp.length !== 6) {
        alert("Please enter the 6-digit verification code.");
        return;
    }

    // TODO: Add C# API fetch call here later
    console.log("Simulating checking OTP:", otp);

    alert("Email verified and updated successfully!");
    closeAllModals();
    loadDashboardData(); // Refresh UI
}


// ==========================================
// 2. PAGE LOAD SETUP
// ==========================================
document.addEventListener("DOMContentLoaded", function() {

    // Bind tabs
    document.querySelectorAll(".settings-tabs .tab").forEach(tab => {
        tab.addEventListener("click", (e) => openTab(e, tab.getAttribute("onclick").split("'")[1]));
    });

    // Fetch data from C# backend when page opens
    loadDashboardData(); 

    // LOGO PREVIEW & UPLOAD
    const logoInput = document.getElementById("logoUpload");
    if(logoInput){
        logoInput.addEventListener("change", async function(e){
            const file = e.target.files[0];
            if(file){
                const reader = new FileReader();
                reader.onload = function(ev){
                    document.getElementById("logoPreview").src = ev.target.result;
                }
                reader.readAsDataURL(file);
                await uploadLogoToDatabase(file);
            }
        });
    }

    // Close modals if clicking outside the box
    window.addEventListener("click", function(event){
        document.querySelectorAll(".modal-overlay").forEach(modal => {
            if(event.target === modal){
                modal.style.display = "none";
            }
        });
    });

    // Make 'X' close buttons actually close modals
    document.querySelectorAll('.close-btn').forEach(btn => {
        btn.addEventListener('click', closeAllModals);
    });

    // ACCOUNT DETAILS EDIT/VIEW TOGGLE
    let isEditingAccount = false;
    const accountEditBtn = document.getElementById("accountEditBtn");
    if(accountEditBtn){
        accountEditBtn.addEventListener("click", function() {
            const inputs = document.querySelectorAll("#account input:not(#displayEmail)"); // Don't enable the email display!
            if(!isEditingAccount){
                inputs.forEach(input => input.removeAttribute("disabled"));
                accountEditBtn.textContent = "Save Account Changes";
                isEditingAccount = true;
            } else {
                inputs.forEach(input => input.setAttribute("disabled", true));
                accountEditBtn.textContent = "Edit Account";
                isEditingAccount = false;
                openUpdateModal("Are you sure you want to save these changes?");
            }
        });
    }

    // Bind Confirm Button inside the modal
    const confirmBtn = document.getElementById("confirmUpdateBtn");
    if(confirmBtn){
        confirmBtn.addEventListener("click", confirmUpdate);
    }

    // Bind Password form
    document.getElementById('changePasswordForm')?.addEventListener('submit', updatePassword);
});


// ==========================================
// 3. API INTEGRATION FUNCTIONS (BACKEND LOGIC)
// ==========================================

async function loadDashboardData() {
    const userId = getUserId();
    if (userId == 0 || !userId) return; 

    try {
        const response = await fetch(`/api/settings/${userId}`);
        if (!response.ok) throw new Error(`Server Error: ${response.status}`);
        
        const data = await response.json();

        if (document.getElementById('displayEmail')) document.getElementById('displayEmail').value = data.account.email || '';
        if (document.getElementById('displayUserType')) document.getElementById('displayUserType').textContent = data.account.userType || 'Seller';

        if (data.business) {
            if (document.getElementById('businessName')) document.getElementById('businessName').value = data.business.businessName || '';
            if (document.getElementById('businessType')) document.getElementById('businessType').value = data.business.businessType || '';
            if (document.getElementById('businessEmail')) document.getElementById('businessEmail').value = data.business.businessEmail || '';
            if (document.getElementById('businessPhone')) document.getElementById('businessPhone').value = data.business.businessPhone || '';
            if (document.getElementById('taxId')) document.getElementById('taxId').value = data.business.taxId || '';
            if (document.getElementById('businessAddress')) document.getElementById('businessAddress').value = data.business.businessAddress || '';
            
            if (document.getElementById('logoPreview')) {
                if (data.business.logoUrl) {
                    document.getElementById('logoPreview').src = data.business.logoUrl;
                } else if (data.business.logoPath) {
                    document.getElementById('logoPreview').src = data.business.logoPath;
                } else {
                    document.getElementById('logoPreview').src = '/images/new-logo.png';
                }
            }
        }
    } catch (error) {
        console.error('Error loading dashboard:', error);
    }
}

async function saveBusinessProfile() {
    const userId = getUserId();
    const payload = {
        businessName: document.getElementById('businessName')?.value || "",
        businessType: document.getElementById('businessType')?.value || "",
        businessEmail: document.getElementById('businessEmail')?.value || "",
        businessPhone: document.getElementById('businessPhone')?.value || "",
        taxId: document.getElementById('taxId')?.value || "",
        businessAddress: document.getElementById('businessAddress')?.value || ""
    };

    try {
        const response = await fetch(`/api/settings/${userId}/business`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        if (response.ok) return true; 
        
        alert('Failed to save profile to database.');
        return false;
    } catch (error) {
        console.error('Error saving profile:', error);
        return false;
    }
}

async function updatePassword(event) {
    if (event) event.preventDefault();
    const userId = getUserId();

    const payload = {
        currentPassword: document.getElementById('currentPassword')?.value || "",
        newPassword: document.getElementById('newPassword')?.value || "",
        confirmNewPassword: document.getElementById('confirmNewPassword')?.value || ""
    };

    try {
        const response = await fetch(`/api/settings/${userId}/password`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        const result = await response.json();

        if (response.ok) {
            alert(result.message);
            const form = document.getElementById('changePasswordForm');
            if(form) form.reset(); 
        } else {
            alert(`Error: ${result.message}`);
        }
    } catch (error) {
        console.error('Error updating password:', error);
    }
}

async function uploadLogoToDatabase(file) {
    const userId = getUserId();
    const formData = new FormData();
    formData.append('file', file);

    try {
        const response = await fetch(`/api/settings/${userId}/logo`, {
            method: 'POST',
            body: formData 
        });

        if (!response.ok) {
            const result = await response.json();
            alert(`Error uploading logo to server: ${result.message}`);
            return;
        }

        const result = await response.json();
        if (result.logoUrl && document.getElementById('logoPreview')) {
            document.getElementById('logoPreview').src = result.logoUrl;
        }
    } catch (error) {
        console.error('Error uploading logo:', error);
    }
}
