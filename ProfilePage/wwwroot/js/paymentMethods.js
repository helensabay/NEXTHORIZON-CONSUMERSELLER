/* =============================================================
   PAYMENT METHODS - COMPLETE UPDATED LOGIC
   ============================================================= */

let editingCardId = null;

document.addEventListener('DOMContentLoaded', async function () {
    const elements = {
        regionInput: document.getElementById('billingRegionInput'),
        regionList: document.getElementById('billingRegionList'),
        provinceInput: document.getElementById('billingProvinceInput'),
        provinceList: document.getElementById('billingProvinceList'),
        cityInput: document.getElementById('billingCityInput'),
        cityList: document.getElementById('billingCityList'),
        barangayInput: document.getElementById('billingBarangayInput'),
        barangayList: document.getElementById('billingBarangayList'),
        postalInput: document.getElementById('billingPostalInput'),
        streetInput: document.getElementById('billingStreetInput'),
        houseInput: document.getElementById('billingHouseInput'),
        // Matches the ID in your HTML: billingBuildingInput
        buildingInput: document.getElementById('billingBuildingInput'),
        cardNumber: document.getElementById('cardNumber'),
        cardHolder: document.getElementById('cardHolderName'),
        cvv: document.getElementById('cardCvv'),
        expMonth: document.getElementById('expiryMonth'),
        expYear: document.getElementById('expiryYear'),
        submitBtn: document.getElementById('mainSubmitBtn'),
        paymentForm: document.getElementById('paymentForm')
    };

    let addressData = {};
    const regionNames = {
        "01": "Region I", "02": "Region II", "03": "Region III", "4A": "Region IV-A",
        "4B": "Region IV-B", "05": "Region V", "06": "Region VI", "07": "Region VII",
        "08": "Region VIII", "09": "Region IX", "10": "Region X", "11": "Region XI",
        "12": "Region XII", "13": "Region XIII", "ARMM": "ARMM", "CAR": "CAR", "NCR": "NCR", "NIR": "NIR"
    };

    async function loadData() {
        try {
            const response = await fetch('/philippine_provinces_cities_municipalities_and_barangays_2016.json');
            addressData = await response.json();
            populateDatalist(elements.regionList, Object.keys(addressData), regionNames);
        } catch (e) { console.error("Error loading address data", e); }
    }

    // --- Cascading Logic for Datalists ---
    elements.regionInput.addEventListener('change', function () {
        const code = getCodeByValue(this.value, regionNames);
        const provinces = addressData[code] ? Object.keys(addressData[code].province_list) : [];
        populateDatalist(elements.provinceList, provinces);
        elements.provinceInput.value = '';
    });

    elements.provinceInput.addEventListener('change', function () {
        const rCode = getCodeByValue(elements.regionInput.value, regionNames);
        const pName = this.value;
        const cities = addressData[rCode]?.province_list[pName]?.municipality_list ?
            Object.keys(addressData[rCode].province_list[pName].municipality_list) : [];
        populateDatalist(elements.cityList, cities);
        elements.cityInput.value = '';
    });

    elements.cityInput.addEventListener('change', function () {
        const rCode = getCodeByValue(elements.regionInput.value, regionNames);
        const pName = elements.provinceInput.value;
        const cName = this.value;
        const barangays = addressData[rCode]?.province_list[pName]?.municipality_list[cName]?.barangay_list || [];
        populateDatalist(elements.barangayList, barangays);
        elements.barangayInput.value = '';
    });

    // --- Form Submission ---
    if (elements.submitBtn) {
        elements.submitBtn.addEventListener('click', function (e) {
            e.preventDefault();

            // Monochrome Validation Logic
            let isValid = true;
            const requiredFields = [elements.cardNumber, elements.cardHolder, elements.regionInput, elements.cvv];

            requiredFields.forEach(el => {
                if (!el.value || !el.value.trim() || el.value === "MM" || el.value === "YYYY") {
                    el.style.borderColor = "#000";
                    isValid = false;
                } else {
                    el.style.borderColor = "#ccc";
                }
            });

            if (!isValid) {
                Swal.fire({
                    title: 'REQUIRED FIELDS',
                    text: 'Please fill in all card and address details.',
                    icon: 'warning',
                    iconColor: '#000',
                    confirmButtonColor: '#000',
                    confirmButtonText: 'RETRY'
                });
                return;
            }

            const cardData = {
                id: editingCardId || Date.now(),
                brand: elements.cardNumber.value.startsWith('4') ? "Visa" : "Mastercard",
                last4: elements.cardNumber.value.replace(/\s+/g, '').slice(-4),
                expiry: `${elements.expMonth.value}/${elements.expYear.value.slice(-2)}`,
                name: elements.cardHolder.value,
                region: elements.regionInput.value,
                province: elements.provinceInput.value,
                city: elements.cityInput.value,
                barangay: elements.barangayInput.value,
                postal: elements.postalInput.value,
                house: elements.houseInput.value,
                building: elements.buildingInput ? elements.buildingInput.value : '',
                street: elements.streetInput.value
            };

            if (editingCardId) {
                updateExistingCardUI(cardData);
            } else {
                addNewCardToUI(cardData);
            }

            Swal.fire({
                title: editingCardId ? 'CARD UPDATED' : 'CARD SAVED',
                text: 'Your payment information has been secured.',
                icon: 'success',
                iconColor: '#000',
                confirmButtonColor: '#000',
                confirmButtonText: 'DONE'
            });

            editingCardId = null;
            togglePaymentForm();
            elements.paymentForm.reset();
        });
    }
    loadData();
});

/* =============================================================
   UI RENDER LOGIC (MONOCHROME DISTINCTION)
   ============================================================= */

function addNewCardToUI(card) {
    const viewSection = document.getElementById('payment-view-section');
    const emptyState = viewSection.querySelector('.empty-state-container');

    const cardHtml = `
        <div class="saved-card-item non-default" id="card-${card.id}" data-card='${JSON.stringify(card).replace(/'/g, "&apos;")}'>
            <div class="card-info-left">
                <i class="bi bi-credit-card-2-front" style="color: #000;"></i>
                <div class="card-details">
                    <span class="card-brand">${card.brand} Ending in ${card.last4}</span>
                    <span class="card-expiry">Expires ${card.expiry}</span>
                </div>
            </div>
            <div class="card-actions-right">
                <button class="btn-edit-card" title="Edit" onclick="editCard(this)">
                    <i class="bi bi-pencil" style="color: #000;"></i>
                </button>
                <button class="btn-set-default" onclick="setDefault(event, ${card.id})">SET DEFAULT</button>
                <button class="btn-remove" onclick="removeCard(event, ${card.id})">Remove</button>
            </div>
        </div>`;

    if (emptyState) {
        viewSection.innerHTML = `
            <div class="section-header-row mb-4">
                <h2 class="section-title">SAVED PAYMENT METHODS</h2>
                <button class="btn-primary-action" onclick="prepareAddForm()">
                    <i class="bi bi-plus-lg"></i> ADD PAYMENT METHOD
                </button>
            </div>
            ${cardHtml}`;
        setDefault(null, card.id);
    } else {
        viewSection.insertAdjacentHTML('beforeend', cardHtml);
    }
}

function updateExistingCardUI(card) {
    const cardElem = document.getElementById(`card-${card.id}`);
    if (cardElem) {
        cardElem.setAttribute('data-card', JSON.stringify(card).replace(/'/g, "&apos;"));
        cardElem.querySelector('.card-brand').innerText = `${card.brand} Ending in ${card.last4}`;
        cardElem.querySelector('.card-expiry').innerText = `Expires ${card.expiry}`;
    }
}

function editCard(btn) {
    const cardItem = btn.closest('.saved-card-item');
    const data = JSON.parse(cardItem.getAttribute('data-card'));
    editingCardId = data.id;

    togglePaymentForm();

    document.getElementById('cardNumber').value = "**** **** **** " + data.last4;
    document.getElementById('cardHolderName').value = data.name;
    document.getElementById('expiryMonth').value = data.expiry.split('/')[0];
    document.getElementById('expiryYear').value = "20" + data.expiry.split('/')[1];
    document.getElementById('billingRegionInput').value = data.region;
    document.getElementById('billingProvinceInput').value = data.province;
    document.getElementById('billingCityInput').value = data.city;
    document.getElementById('billingBarangayInput').value = data.barangay;
    document.getElementById('billingPostalInput').value = data.postal;
    document.getElementById('billingStreetInput').value = data.street;
    document.getElementById('billingHouseInput').value = data.house || '';
    document.getElementById('billingBuildingInput').value = data.building || '';

    document.getElementById('mainSubmitBtn').innerText = "UPDATE PAYMENT METHOD";
}

function setDefault(event, id) {
    if (event) event.stopPropagation();

    document.querySelectorAll('.saved-card-item').forEach(el => {
        el.classList.remove('is-default');
        el.classList.add('non-default');

        const actions = el.querySelector('.card-actions-right');
        const badge = actions.querySelector('.badge-default');

        if (badge) {
            badge.remove();
            const btn = document.createElement('button');
            btn.className = 'btn-set-default';
            btn.innerText = 'SET DEFAULT';
            const cardId = el.id.replace('card-', '');
            btn.onclick = (e) => setDefault(e, cardId);
            actions.insertBefore(btn, actions.querySelector('.btn-remove'));
        }
    });

    const current = document.getElementById('card-' + id);
    if (current) {
        current.classList.remove('non-default');
        current.classList.add('is-default');

        const actions = current.querySelector('.card-actions-right');
        const btn = actions.querySelector('.btn-set-default');
        if (btn) btn.remove();

        const badge = document.createElement('span');
        badge.className = 'badge-default';
        badge.innerText = 'DEFAULT';
        actions.insertBefore(badge, actions.querySelector('.btn-remove'));
    }
}

function removeCard(event, id) {
    if (event) event.stopPropagation();

    Swal.fire({
        title: 'REMOVE CARD?',
        text: "Are you sure you want to delete this payment method?",
        icon: 'warning',
        iconColor: '#000',
        showCancelButton: true,
        confirmButtonColor: '#000',
        cancelButtonColor: '#fff',
        confirmButtonText: 'YES, DELETE',
        cancelButtonText: 'CANCEL',
        reverseButtons: true,
        customClass: {
            cancelButton: 'swal-cancel-custom'
        }
    }).then((result) => {
        if (result.isConfirmed) {
            const cardElem = document.getElementById('card-' + id);
            const wasDefault = cardElem.classList.contains('is-default');
            cardElem.remove();

            const remaining = document.querySelectorAll('.saved-card-item');
            if (remaining.length === 0) {
                location.reload();
            } else if (wasDefault) {
                setDefault(null, remaining[0].id.replace('card-', ''));
            }

            Swal.fire({
                title: 'DELETED',
                icon: 'success',
                iconColor: '#000',
                confirmButtonColor: '#000',
                confirmButtonText: 'OK'
            });
        }
    });
}

function togglePaymentForm() {
    const v = document.getElementById('payment-view-section'),
        f = document.getElementById('payment-form-section');
    const isHidden = (f.style.display === 'none' || f.style.display === '');

    v.style.display = isHidden ? 'none' : 'block';
    f.style.display = isHidden ? 'block' : 'none';

    document.querySelector('.profile-card-content').scrollIntoView({ behavior: 'smooth' });
}

function prepareAddForm() {
    editingCardId = null;
    document.getElementById('paymentForm').reset();
    document.getElementById('mainSubmitBtn').innerText = "SAVE PAYMENT METHOD";
    togglePaymentForm();
}

function populateDatalist(list, items, mapping = null) {
    list.innerHTML = '';
    items.sort().forEach(item => {
        const opt = document.createElement('option');
        opt.value = mapping ? mapping[item] : item;
        list.appendChild(opt);
    });
}

function getCodeByValue(val, mapping) {
    return Object.keys(mapping).find(key => mapping[key] === val) || val;
}