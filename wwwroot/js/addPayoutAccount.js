document.addEventListener("DOMContentLoaded", function () {

    // Existing code for account type & e-wallet input...
    const numberInput = document.getElementById("ewalletNumber");

    if (numberInput) {
        numberInput.addEventListener("input", function () {
            this.value = this.value.replace(/[^0-9]/g, "");
            if (this.value.length >= 1 && this.value[0] !== "0") {
                this.value = "";
            }
            if (this.value.length >= 2 && this.value.substring(0,2) !== "09") {
                this.value = "09";
            }
        });
    }

    const accountType = document.getElementById("accountType");
    const cardFields = document.getElementById("cardFields");
    const ewalletFields = document.getElementById("ewalletFields");
    const bankFields = document.getElementById("bankFields");

    if(accountType){
        accountType.addEventListener("change", function(){
            cardFields.style.display = "none";
            ewalletFields.style.display = "none";
            bankFields.style.display = "none";

            if(this.value === "card"){
                cardFields.style.display = "block";
            }
            if(this.value === "ewallet"){
                ewalletFields.style.display = "block";
            }
            if(this.value === "bank"){
                bankFields.style.display = "block";
            }
        });
    }

    // ===== Add Account Button =====
    const addBtn = document.getElementById("addAccountBtn");
    if(addBtn){
        addBtn.addEventListener("click", function (e) {
            e.preventDefault();
            
            // Validate form before submitting
            const form = document.getElementById("payoutAccountForm");
            const accountTypeValue = accountType ? accountType.value : '';
            
            if (!accountTypeValue) {
                if (window.Modal) {
                    Modal.warning('Please select an account type');
                }
                return;
            }
            
            // Submit the form
            if (form) {
                form.submit();
            }
        });
    }

});