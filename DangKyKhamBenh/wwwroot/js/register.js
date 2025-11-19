$(function () {

    function showErrorToast(messageHtml) {
        var $toast = $('#error-toast');
        if ($toast.length === 0) return;
        $('#error-toast-body').html(messageHtml);
        $toast.stop(true, true).fadeIn(200);
        setTimeout(() => $toast.fadeOut(300), 15000);
    }

    // Đóng toast
    $('#error-toast-close').on('click', function () {
        $('#error-toast').fadeOut(200);
    });

    // Lấy lỗi server-side
    var $vs = $('#validation-summary');
    if ($vs.length) {
        var items = [];
        $vs.find('li').each(function () {
            var text = $(this).text().trim();
            if (text.length) items.push(text);
        });
        if (items.length > 0) {
            var html = '<ul class="mb-0 ps-3">' + items.map(t => '<li>' + t + '</li>').join('') + '</ul>';
            showErrorToast(html);
        }
    }

    // Client-side validation
    $('#frmRegister').on('submit', function (e) {
        e.preventDefault();
        var valid = true;
        $('.form-control').removeClass('is-invalid');
        $('.invalid-feedback').text('').hide();

        var email = $('input[name="Email"]').val().trim();
        var emailPattern = /^[a-zA-Z0-9._%+-]+@[a-zA-Z.-]+\.[a-zA-Z]{2,}$/;
        if (!email) { valid = false; $('input[name="Email"]').addClass('is-invalid'); $('#email-error').text('Email không được để trống.').show(); }
        else if (!emailPattern.test(email)) { valid = false; $('input[name="Email"]').addClass('is-invalid'); $('#email-error').text('Email không hợp lệ.').show(); }

        var pass = $('input[name="Password"]').val();
        if (!pass || pass.length < 6) { valid = false; $('input[name="Password"]').addClass('is-invalid'); $('#password-error').text('Mật khẩu phải có ít nhất 6 ký tự.').show(); }

        var confirmPass = $('input[name="ConfirmPassword"]').val();
        if (!confirmPass) { valid = false; $('input[name="ConfirmPassword"]').addClass('is-invalid'); $('#confirm-error').text('Vui lòng xác nhận mật khẩu.').show(); }
        else if (confirmPass !== pass) { valid = false; $('input[name="ConfirmPassword"]').addClass('is-invalid'); $('#confirm-error').text('Mật khẩu xác nhận không khớp.').show(); }

        var phone = $('input[name="PhoneNumber"]').val().trim();
        if (!phone) { valid = false; $('input[name="PhoneNumber"]').addClass('is-invalid'); $('#phone-error').text('Số điện thoại không được để trống.').show(); }
        else if (!/^[0-9]{10,}$/.test(phone)) { valid = false; $('input[name="PhoneNumber"]').addClass('is-invalid'); $('#phone-error').text('Số điện thoại phải có ít nhất 10 chữ số.').show(); }

        var dobInput = $('input[name="DateOfBirth"]').val();
        if (!dobInput) { valid = false; $('input[name="DateOfBirth"]').addClass('is-invalid'); $('#dob-error').text('Vui lòng nhập ngày sinh.').show(); }
        else {
            var dob = new Date(dobInput), today = new Date(), age = today.getFullYear() - dob.getFullYear();
            var m = today.getMonth() - dob.getMonth();
            if (m < 0 || (m === 0 && today.getDate() < dob.getDate())) age--;
            if (age < 15) { valid = false; $('input[name="DateOfBirth"]').addClass('is-invalid'); $('#dob-error').text('Bạn phải từ 15 tuổi trở lên.').show(); }
        }

        if (!valid) { showErrorToast('Vui lòng kiểm tra lại các trường được đánh dấu màu đỏ.'); return; }
        this.submit();
    });
});
